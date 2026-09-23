#!/usr/bin/env bash
# Forge end-to-end test suite.
#
#   test/run.sh                  # build + run every scenario
#   test/run.sh path cache       # only scenarios whose id contains a filter
#   test/run.sh --list           # list the scenarios and exit
#   test/run.sh --keep           # keep the scratch workspace (auto-kept on failure)
#   test/run.sh --no-build       # skip `dotnet build` and use the existing build
#   test/run.sh --verbose        # stream CLI output instead of capturing it
#
# The suite drives the dev build directly (`dotnet bin/Release/net10.0/forge.dll`)
# against a throwaway workspace, so the installed `forge` is never touched.
#
# Adding a scenario: drop a file in test/scenarios/ named <id>.sh defining
# `scenario_<id_with_underscores>()`. Keep its variables `local`; use $WORK for
# scratch space (already created per run).
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FORGE_DLL="$REPO/bin/Release/net10.0/forge.dll"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/forge-e2e.XXXXXX")"
KEEP=0
DO_BUILD=1
LIST=0
VERBOSE=0
FILTERS=()

for arg in "$@"; do
  case "$arg" in
    --list) LIST=1 ;;
    --keep) KEEP=1 ;;
    --no-build) DO_BUILD=0 ;;
    --verbose | -v) VERBOSE=1 ;;
    -h | --help)
      sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*) echo "unknown option: $arg" >&2; exit 2 ;;
    *) FILTERS+=("$arg") ;;
  esac
done

FORGE_CMD=(dotnet "$FORGE_DLL")

# Scenarios that put stubs on PATH (cmake/ctest/conan/ccache) restore it
# themselves, but a scenario that returns early could leak one into the next.
# Reset to the baseline before every scenario.
BASELINE_PATH="$PATH"

# Keep the shared dependency cache out of the user's home and inside the
# scratch workspace, so scenarios are deterministic and nothing leaks between
# runs. A scenario may override it (and must restore it).
export FORGE_CACHE_DIR="$WORK/.dependency-cache"

cleanup() {
  stop_http_server
  if [[ "$KEEP" -eq 1 || "$FAILED" -gt 0 ]]; then
    printf '\nscratch workspace: %s\n' "$WORK"
  else
    rm -rf "$WORK"
  fi
}
trap cleanup EXIT

# shellcheck source=lib.sh
source "$REPO/test/lib.sh"

# --- collect scenarios -------------------------------------------------------

SELECTED=()
for file in "$REPO"/test/scenarios/*.sh; do
  id="$(basename "$file" .sh)"
  if [[ ${#FILTERS[@]} -gt 0 ]]; then
    match=0
    for f in "${FILTERS[@]}"; do [[ "$id" == *"$f"* ]] && match=1; done
    [[ "$match" -eq 1 ]] || continue
  fi
  SELECTED+=("$id")
done

if [[ "$LIST" -eq 1 ]]; then
  for id in "${SELECTED[@]}"; do echo "$id"; done
  exit 0
fi

if [[ ${#SELECTED[@]} -eq 0 ]]; then
  echo "no scenarios matched: ${FILTERS[*]:-<none>}" >&2
  exit 2
fi

# --- prerequisites -----------------------------------------------------------

for tool in cmake git tar; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "missing prerequisite: $tool" >&2
    exit 2
  fi
done

# --- build -------------------------------------------------------------------

if [[ "$DO_BUILD" -eq 1 ]]; then
  printf 'building forge'
  if out="$(cd "$REPO" && dotnet build -c Release 2>&1)"; then
    if grep -qE '[0-9]+ Warning' <<<"$out" && ! grep -qE '0 Warning' <<<"$out"; then
      printf ' — \033[31mwarnings\033[0m\n'
      grep -E 'warning' <<<"$out" | head -5
      exit 1
    fi
    printf ' — \033[32mclean\033[0m\n'
  else
    printf ' — \033[31mfailed\033[0m\n'
    tail -15 <<<"$out"
    exit 1
  fi
  [[ "$VERBOSE" -eq 1 ]] && grep -E 'Build succeeded|Warning\(s\)' <<<"$out"
fi

# --- run ---------------------------------------------------------------------

printf 'forge e2e — %d scenario(s), workspace %s\n' "${#SELECTED[@]}" "$WORK"

SUMMARY=()
for id in "${SELECTED[@]}"; do
  fn="scenario_${id//-/_}"
  before_passed=$PASSED
  before_failed=$FAILED

  printf '\n== %s ==\n' "$id"
  # A scenario that returned early may have left its local HTTP server or stub
  # PATH entries behind; never let them leak into the next scenario.
  stop_http_server
  export PATH="$BASELINE_PATH"
  # shellcheck disable=SC1090
  source "$REPO/test/scenarios/$id.sh"
  "$fn"

  SUMMARY+=("$(printf '%-24s %2d passed, %d failed' "$id" \
    "$((PASSED - before_passed))" "$((FAILED - before_failed))")")
done

printf '\n--- summary ---\n'
for line in "${SUMMARY[@]}"; do echo "$line"; done
printf '\n%d passed, %d failed, %d skipped\n' "$PASSED" "$FAILED" "$SKIPPED"
[[ "$FAILED" -eq 0 ]]
