# Shared helpers for the Forge end-to-end suite.
#
# Sourced by run.sh; scenario files use the helpers below and must keep their
# variables `local` (scenarios share the runner's shell so the pass/fail
# counters work).

PASSED=0
FAILED=0
SKIPPED=0

pass() { printf '  \033[32mok\033[0m   %s\n' "$*"; PASSED=$((PASSED + 1)); }
fail() {
  printf '  \033[31mFAIL\033[0m %s\n' "$*"
  FAILED=$((FAILED + 1))
  # Kept separately: the per-scenario lines scroll away, and a long run is
  # usually read from its tail.
  FAILURES+=("${CURRENT_SCENARIO:-?}: $*")
}
skip() { printf '  \033[2mskip\033[0m %s\n' "$*"; SKIPPED=$((SKIPPED + 1)); }

# Strip ANSI escapes and collapse whitespace/newlines: Spectre wraps its output
# at the console width, so a message can be split mid-phrase.
# GNU sed takes `sed -i`; BSD/macOS sed needs an explicit (empty) backup
# suffix. Everything goes through this so the suite is portable.
sed_in_place() { # <expression> <file...>
  local expression="$1"
  shift
  if sed --version >/dev/null 2>&1; then
    sed -i "$expression" "$@"
  else
    sed -i '' "$expression" "$@"
  fi
}

flatten() { sed $'s/\033\\[[0-9;]*m//g' | tr '\n' ' ' | tr -s ' '; }

# The CLI under test (an array, so it can be `dotnet <dll>` or a binary path).
forge() { "${FORGE_CMD[@]}" "$@"; }
forge_in() { # <dir> [args...]
  local dir="$1"
  shift
  (cd "$dir" && "${FORGE_CMD[@]}" "$@")
}

# Like forge_in, but bounded: a command that hangs (a watch loop, a stuck
# download) fails the scenario instead of hanging the whole suite.
forge_in_timeout() { # <seconds> <dir> [args...]
  local seconds="$1" dir="$2"
  shift 2
  (cd "$dir" && timeout "$seconds" "${FORGE_CMD[@]}" "$@")
}

# --- assertions --------------------------------------------------------------

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

assert_exit() { # <expected code> <label> <command...>
  local want="$1" label="$2"
  shift 2
  local got=0 output
  # Keep the output: when the exit code is unexpected, the reason is almost
  # always in there, and a bare "exit 1, wanted 0" costs a debugging session.
  output="$("$@" 2>&1)" || got=$?
  if [[ "$got" == "$want" ]]; then
    pass "$label"
  else
    fail "$label (exit $got, wanted $want): $(flatten <<<"$output" | tail -c 200)"
  fi
}

assert_order() { # <text> <label> <needle...> — the needles must appear in order
  local text="$1" label="$2"
  shift 2
  local previous=-1 needle position
  for needle in "$@"; do
    # `|| true`: a missing needle must be reported, not abort the scenario
    # under `set -e`.
    position="$(grep -boF "$needle" <<<"$text" | head -1 | cut -d: -f1 || true)"
    if [[ -z "$position" ]]; then
      fail "$label (missing '$needle')"
      return
    fi
    if ((position < previous)); then
      fail "$label ('$needle' is out of order)"
      return
    fi
    previous="$position"
  done
  pass "$label"
}

assert_exists() { # <path> <label>
  if [[ -e "$1" ]]; then
    pass "$2"
  else
    fail "$2 (missing $1)"
  fi
}

assert_missing() { # <path> <label>
  if [[ -e "$1" ]]; then
    fail "$2 (found $1)"
  else
    pass "$2"
  fi
}

# --- local HTTP server (download/fetch scenarios) ----------------------------

HTTP_PID=""
HTTP_PORT=""

wait_for_http() { # <port>
  local tries=0
  until python3 -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:$1/', timeout=1)" >/dev/null 2>&1; do
    tries=$((tries + 1))
    [[ "$tries" -ge 50 ]] && return 1
    sleep 0.1
  done
  return 0
}

start_http_server() { # <dir> — sets HTTP_PORT
  if ! command -v python3 >/dev/null 2>&1; then
    return 1
  fi
  HTTP_PORT=$((8700 + RANDOM % 200))
  (cd "$1" && exec python3 -m http.server "$HTTP_PORT" >/dev/null 2>&1) &
  HTTP_PID=$!
  wait_for_http "$HTTP_PORT" || return 1
  return 0
}

stop_http_server() {
  if [[ -n "$HTTP_PID" ]]; then
    kill "$HTTP_PID" 2>/dev/null || true
    wait "$HTTP_PID" 2>/dev/null || true
  fi
  HTTP_PID=""
  HTTP_PORT=""
}

# --- fixtures ----------------------------------------------------------------

# A library with a CMakeLists and one header, and a consumer that links it —
# either through a local `path` or a `git` dependency. $1 is the directory.
make_dep_project() {
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

# A minimal executable project (no dependencies). $1 = directory, $2 = name.
make_plain_project() {
  local root="$1" name="$2"
  mkdir -p "$root/src"
  cat >"$root/forge.lua" <<LUA
return {
  project = { name = "$name", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("hello from %s\\n"); return 0; }\n' "$name" \
    >"$root/src/main.cpp"
}
