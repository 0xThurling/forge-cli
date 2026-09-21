#!/usr/bin/env bash
# Build the Forge CLI and verify it end-to-end — no install, no sudo.
#
#   ./dev.sh                  # build + every scenario
#   ./dev.sh path cache       # only the named scenarios
#   ./dev.sh --keep           # keep the scratch workspace for inspection
#
# Scenarios drive the *dev* build directly
# (dotnet bin/Release/net10.0/forge.dll) against a throwaway workspace under
# $TMPDIR, so the installed `forge` is never touched.
#
#   build    dotnet build -c Release, failing on warnings
#   path     local-path dependency: SOURCE_DIR emitted, nothing fetched, runs
#   missing  a path that does not exist is reported with its resolved location
#   git      git dependencies still fetch and link (local file:// repo)
#   cache    a build/ configured for another source tree is regenerated
#   mirror   library header install: sibling includes verbatim, others rewritten
#   lua      a build script's cmakeOptions reach the generated CMake (pre/post)
#   fp       (only with a ../fp checkout) fp's mirror stays byte-identical and
#            its git status does not grow
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FORGE_DLL="$REPO/bin/Release/net10.0/forge.dll"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/forge-dev.XXXXXX")"
KEEP=0
FAILED=0
PASSED=0
SCENARIOS=()

for arg in "$@"; do
  case "$arg" in
    --keep) KEEP=1 ;;
    -h | --help) sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) SCENARIOS+=("$arg") ;;
  esac
done

cleanup() {
  if [[ "$KEEP" -eq 1 || "$FAILED" -gt 0 ]]; then
    printf '\nscratch workspace: %s\n' "$WORK"
  else
    rm -rf "$WORK"
  fi
}
trap cleanup EXIT

# --- helpers -----------------------------------------------------------------

forge_in() { # <dir> [args...]
  local dir="$1"
  shift
  (cd "$dir" && dotnet "$FORGE_DLL" "$@")
}

pass() { printf '  \033[32mok\033[0m   %s\n' "$*"; PASSED=$((PASSED + 1)); }
fail() { printf '  \033[31mFAIL\033[0m %s\n' "$*"; FAILED=$((FAILED + 1)); }
skip() { printf '  \033[2mskip\033[0m %s\n' "$*"; }

assert_contains() { # <file> <needle> <label>
  if grep -qF -- "$2" "$1" 2>/dev/null; then
    pass "$3"
  else
    fail "$3 (no '$2' in $1)"
  fi
}

assert_lacks() { # <file> <needle> <label>
  if grep -qF -- "$2" "$1" 2>/dev/null; then
    fail "$3 (found '$2' in $1)"
  else
    pass "$3"
  fi
}

assert_runs() { # <binary> <expected output> <label>
  local out
  if out="$("$1" 2>&1)" && [[ "$out" == *"$2"* ]]; then
    pass "$3"
  else
    fail "$3 (got '$out')"
  fi
}

# Strip ANSI escapes and collapse whitespace/newlines: Spectre wraps its
# output at the console width, so a message can be split mid-phrase.
flatten() { sed $'s/\033\\[[0-9;]*m//g' | tr '\n' ' ' | tr -s ' '; }

wants() { # scenario name requested?
  [[ ${#SCENARIOS[@]} -eq 0 ]] && return 0
  local s
  for s in "${SCENARIOS[@]}"; do [[ "$s" == "$1" ]] && return 0; done
  return 1
}

# A consumer project with a local-path dependency, plus the library it points
# at. $1 is the workspace subdirectory to create it under.
make_path_project() {
  local root="$1"
  mkdir -p "$root/lib/include" "$root/app/src"
  cat >"$root/lib/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(demo_lib LANGUAGES CXX)
add_library(demo_lib INTERFACE)
target_include_directories(demo_lib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int demo_value() { return 42; }\n' \
    >"$root/lib/include/demo.h"
  cat >"$root/app/forge.lua" <<'LUA'
return {
  project = { name = "demo_app", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      demo = { path = "../lib", target = "demo_lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <demo.h>\n#include <cstdio>\nint main() { std::printf("demo_value=%%d\\n", demo_value()); return 0; }\n' \
    >"$root/app/src/main.cpp"
}

# --- scenarios ---------------------------------------------------------------

scenario_build() {
  printf '\n== build ==\n'
  local out
  if out="$(cd "$REPO" && dotnet build -c Release 2>&1)"; then
    if grep -qE 'Warning\(s\)' <<<"$out" && ! grep -qE '0 Warning\(s\)' <<<"$out"; then
      fail "dotnet build reported warnings"
      grep -E 'warning' <<<"$out" | head -5
    else
      pass "dotnet build -c Release (clean)"
    fi
  else
    fail "dotnet build -c Release"
    tail -15 <<<"$out"
  fi
}

scenario_path() {
  printf '\n== path dependency ==\n'
  local root="$WORK/path"
  make_path_project "$root"

  if ! forge_in "$root/app" build >/dev/null 2>&1; then
    fail "builds with a path dependency"
    return
  fi
  pass "builds with a path dependency"

  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    'FetchContent_Declare(demo SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../lib")' \
    "emits SOURCE_DIR for the local checkout"
  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    'target_link_libraries(demo_app PRIVATE demo_lib)' \
    "links the declared target"

  if [[ -d "$root/app/build/_deps/demo-src" ]]; then
    fail "nothing is fetched (found _deps/demo-src)"
  else
    pass "nothing is fetched"
  fi
  assert_runs "$root/app/build/demo_app" "demo_value=42" "runs against the local library"
}

scenario_missing() {
  printf '\n== missing path ==\n'
  local root="$WORK/missing"
  make_path_project "$root"
  sed -i 's|path = "../lib"|path = "../nope"|' "$root/app/forge.lua"

  local out flat
  out="$(forge_in "$root/app" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "points at '../nope', which does not exist" <<<"$flat"; then
    pass "warns with the offending path"
  else
    fail "warns with the offending path"
  fi
  if grep -qF "$root/nope" <<<"$flat"; then
    pass "prints the resolved location"
  else
    fail "prints the resolved location"
  fi
}

scenario_git() {
  printf '\n== git dependency ==\n'
  local root="$WORK/git"
  make_path_project "$root"
  rm -rf "$root/app/build" "$root/app/.config" "$root/app/CMakeLists.txt"

  git -C "$root/lib" init -q
  git -C "$root/lib" add -A
  git -C "$root/lib" -c user.email=dev@forge -c user.name=dev commit -qm init
  git -C "$root/lib" tag v1

  cat >"$root/app/forge.lua" <<LUA
return {
  project = { name = "demo_app", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      demo = { git = "file://$root/lib", tag = "v1", target = "demo_lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA

  if ! forge_in "$root/app" build >/dev/null 2>&1; then
    fail "builds with a git dependency"
    return
  fi
  pass "builds with a git dependency"

  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    "FetchContent_Declare(demo GIT_REPOSITORY \"file://$root/lib\" GIT_TAG \"v1\")" \
    "emits GIT_REPOSITORY/GIT_TAG"
  if [[ -d "$root/app/build/_deps/demo-src" ]]; then
    pass "fetches the repository"
  else
    fail "fetches the repository"
  fi
  assert_runs "$root/app/build/demo_app" "demo_value=42" "runs against the fetched library"
}

scenario_cache() {
  printf '\n== stale build cache ==\n'
  local root="$WORK/cache"
  make_path_project "$root"
  forge_in "$root/app" build >/dev/null 2>&1 || true

  sed -i 's|^CMAKE_HOME_DIRECTORY:INTERNAL=.*|CMAKE_HOME_DIRECTORY:INTERNAL=/nonexistent/old-checkout|' \
    "$root/app/build/CMakeCache.txt"
  if grep -qF "CMAKE_HOME_DIRECTORY:INTERNAL=/nonexistent/old-checkout" \
    "$root/app/build/CMakeCache.txt"; then
    pass "stale cache planted"
  else
    fail "stale cache planted (sed did not apply)"
    return
  fi

  local out flat
  out="$(forge_in "$root/app" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "regenerating the cache" <<<"$flat"; then
    pass "detects the mismatch"
  else
    fail "detects the mismatch"
  fi
  if grep -qF "CMAKE_HOME_DIRECTORY:INTERNAL=$root/app" "$root/app/build/CMakeCache.txt"; then
    pass "reconfigures for the current source tree"
  else
    fail "reconfigures for the current source tree"
  fi
  assert_runs "$root/app/build/demo_app" "demo_value=42" "still runs afterwards"
}

scenario_mirror() {
  printf '\n== library header install ==\n'
  local root="$WORK/mirror"
  mkdir -p "$root/src/fp" "$root/src/other"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_hdrs", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#pragma once\ninline int b() { return 1; }\n' >"$root/src/fp/b.hpp"
  printf '#pragma once\n#include "b.hpp"\ninline int sibling() { return b(); }\n' \
    >"$root/src/fp/sibling.hpp"
  printf '#pragma once\n#include "fp/b.hpp"\ninline int pathstyle() { return b(); }\n' \
    >"$root/src/fp/pathstyle.hpp"
  printf '#pragma once\n#include "b.hpp"\ninline int cross() { return b(); }\n' \
    >"$root/src/other/cross.hpp"
  printf 'namespace demo { int answer() { return 42; } }\n' >"$root/src/demo_hdrs.cpp"

  forge_in "$root" build >/dev/null 2>&1 || true

  assert_contains "$root/include/demo_hdrs/fp/sibling.hpp" '#include "b.hpp"' \
    "sibling include copied verbatim"
  assert_lacks "$root/include/demo_hdrs/fp/sibling.hpp" 'demo_hdrs/fp/b.hpp' \
    "sibling include not prefixed"
  assert_contains "$root/include/demo_hdrs/fp/pathstyle.hpp" \
    '#include "demo_hdrs/fp/b.hpp"' "path-style include rewritten"
  assert_contains "$root/include/demo_hdrs/other/cross.hpp" \
    '#include "demo_hdrs/fp/b.hpp"' "cross-directory include rewritten"
}

scenario_lua() {
  printf '\n== lua build script (cmakeOptions) ==\n'
  local root="$WORK/lua"
  mkdir -p "$root/src" "$root/.config/forge/build"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_lua", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  custom = { DEMO_CUSTOM = "declared" }
}
LUA
  printf 'payload\n' >"$root/payload.txt"
  tar -cf "$root/payload.tar" -C "$root" payload.txt

  cat >"$root/.config/forge/build/setup.lua" <<'LUA'
-- Stands in for a WebGPU-style setup: prepare something, then declare it.
-- The 2-argument extract must default stripComponents rather than throw.
forge.extract("payload.tar", "unpacked")
forge.add_cmake('set(PRE_MARKER "pre")', "pre")
forge.add_cmake('set(POST_MARKER "post")')
return {
  cmakeOptions = {
    variables = { DEMO_SDK = "/opt/demo" },
    findPackages = { "Threads" },
    definitions = { "DEMO_FROM_SCRIPT=7" },
    compileOptions = { "-DDEMO_FLAG=1" },
    linkLibraries = { "m" }
  }
}
LUA
  cat >"$root/src/main.cpp" <<'CPP'
#include <cstdio>
int main() {
#ifndef DEMO_FROM_SCRIPT
#error "the script's definition did not reach the compiler"
#endif
  std::printf("script_def=%d\n", DEMO_FROM_SCRIPT);
  return 0;
}
CPP

  local out
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds with a lua build script"
    tail -5 <<<"$out"
    return
  fi
  pass "builds with a lua build script"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'set(DEMO_CUSTOM "declared")' "custom entries become CMake variables"
  assert_contains "$cmake" 'set(DEMO_SDK "/opt/demo")' "script variables emitted"
  assert_contains "$cmake" 'find_package(Threads REQUIRED)' "find_package emitted"
  assert_contains "$cmake" 'add_compile_definitions(DEMO_FROM_SCRIPT=7)' "definitions emitted"
  assert_contains "$cmake" 'add_compile_options("-DDEMO_FLAG=1")' "compile options emitted"
  assert_contains "$cmake" 'link_libraries(m)' "link libraries emitted"

  # A "pre" snippet must land before the target is created, the default after.
  local pre_line target_line post_line
  pre_line="$(grep -n 'PRE_MARKER' "$cmake" | cut -d: -f1 || true)"
  target_line="$(grep -n 'add_executable' "$cmake" | cut -d: -f1 || true)"
  post_line="$(grep -n 'POST_MARKER' "$cmake" | cut -d: -f1 || true)"
  if [[ -n "$pre_line" && -n "$target_line" && -n "$post_line" &&
    "$pre_line" -lt "$target_line" && "$target_line" -lt "$post_line" ]]; then
    pass "pre snippet before the target, post snippet after"
  else
    fail "pre snippet before the target, post snippet after (pre=$pre_line target=$target_line post=$post_line)"
  fi

  assert_runs "$root/build/demo_lua" "script_def=7" "the definition reached the compiler"

  if [[ -f "$root/unpacked/payload.txt" ]]; then
    pass "forge.extract(archive, dir) defaults stripComponents"
  else
    fail "forge.extract(archive, dir) defaults stripComponents"
  fi
}

scenario_fp() {
  local fp="$REPO/../fp"
  printf '\n== fp integration ==\n'
  if [[ ! -d "$fp/src/fp" ]]; then
    skip "no ../fp checkout"
    return
  fi

  local before after
  before="$(git -C "$fp" status --porcelain | wc -l)"

  if ! forge_in "$fp" build >/dev/null 2>&1; then
    fail "forge build on fp"
    return
  fi
  pass "forge build on fp"

  if diff -r "$fp/src/fp" "$fp/include/forgefp/fp" >/dev/null 2>&1; then
    pass "fp mirror stays byte-identical"
  else
    fail "fp mirror stays byte-identical"
  fi

  after="$(git -C "$fp" status --porcelain | wc -l)"
  if [[ "$after" -le "$before" ]]; then
    pass "fp git status unchanged ($before entries)"
  else
    fail "fp git status grew ($before -> $after)"
  fi
}

# --- run ---------------------------------------------------------------------

printf 'forge dev — workspace %s\n' "$WORK"

for scenario in build path missing git cache mirror lua fp; do
  if wants "$scenario"; then
    "scenario_$scenario"
  fi
done

printf '\n%d passed, %d failed\n' "$PASSED" "$FAILED"
[[ "$FAILED" -eq 0 ]]
