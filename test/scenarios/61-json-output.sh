# `--json` output for the data commands, so CI and editors can consume Forge.
scenario_61_json_output() {
  local root="$WORK/61-json-output"
  mkdir -p "$root/src" "$root/lib/include"
  cat >"$root/lib/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(libjson LANGUAGES CXX)
add_library(libjson INTERFACE)
CMAKE
  printf '#pragma once\ninline int v() { return 1; }\n' >"$root/lib/include/v.h"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_json", type = "library", standard = "17", install_headers = true },
  dependencies = {
    direct = {
      libjson = { path = "lib", target = "libjson" },
      remote = { git = "https://example.com/remote.git", tag = "v1.2.3", target = "remote::remote" }
    },
    conan = { fmt = "10.2.1" }
  },
  resources = { files = {} },
  scripts = { hello = "echo hi" },
  features = { graphics = { enabled = true } }
}
LUA
  printf 'namespace demo { int f() { return 1; } }\n' >"$root/src/lib.cpp"

  json_ok() { # <label> <json>
    if python3 -c "import json,sys; json.loads(sys.argv[1])" "$2" >/dev/null 2>&1; then
      pass "$1"
    else
      fail "$1 (invalid JSON: $(printf '%.80s' "$2"))"
    fi
  }

  local out
  out="$(forge_in "$root" project info --json 2>&1 | tail -1)"
  json_ok "project info --json parses" "$out"
  if [[ "$out" == *'"name":"demo_json"'* && "$out" == *'"type":"library"'* &&
    "$out" == *'"standard":"17"'* && "$out" == *'"testing":false'* ]]; then
    pass "project info reports the project fields"
  else
    fail "project info reports the project fields"
  fi

  out="$(forge_in "$root" project dependencies --json 2>&1 | tail -1)"
  json_ok "project dependencies --json parses" "$out"
  if [[ "$out" == *'"name":"libjson"'* && "$out" == *'"channel":"path"'* &&
    "$out" == *'"name":"remote"'* && "$out" == *'"channel":"git"'* &&
    "$out" == *'"ref":"v1.2.3"'* && "$out" == *'"name":"fmt"'* && "$out" == *'"channel":"conan"'* ]]; then
    pass "project dependencies lists every channel"
  else
    fail "project dependencies lists every channel"
  fi

  # Without the flag the human output is unchanged.
  out="$(forge_in "$root" project info 2>&1)"
  if [[ "$out" == *"Project Name"* && "$out" != *'"name"'* ]]; then
    pass "the human output is unchanged"
  else
    fail "the human output is unchanged"
  fi
}
