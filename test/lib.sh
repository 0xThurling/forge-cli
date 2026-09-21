# Shared helpers for the Forge end-to-end suite.
#
# Sourced by run.sh; scenario files use the helpers below and must keep their
# variables `local` (scenarios share the runner's shell so the pass/fail
# counters work).

PASSED=0
FAILED=0
SKIPPED=0

pass() { printf '  \033[32mok\033[0m   %s\n' "$*"; PASSED=$((PASSED + 1)); }
fail() { printf '  \033[31mFAIL\033[0m %s\n' "$*"; FAILED=$((FAILED + 1)); }
skip() { printf '  \033[2mskip\033[0m %s\n' "$*"; SKIPPED=$((SKIPPED + 1)); }

# Strip ANSI escapes and collapse whitespace/newlines: Spectre wraps its output
# at the console width, so a message can be split mid-phrase.
flatten() { sed $'s/\033\\[[0-9;]*m//g' | tr '\n' ' ' | tr -s ' '; }

# The CLI under test (an array, so it can be `dotnet <dll>` or a binary path).
forge() { "${FORGE_CMD[@]}" "$@"; }
forge_in() { # <dir> [args...]
  local dir="$1"
  shift
  (cd "$dir" && "${FORGE_CMD[@]}" "$@")
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
  local got=0
  "$@" >/dev/null 2>&1 || got=$?
  if [[ "$got" == "$want" ]]; then
    pass "$label"
  else
    fail "$label (exit $got, wanted $want)"
  fi
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
