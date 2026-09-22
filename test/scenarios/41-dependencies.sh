# `forge project dependencies` lists every channel: git, local path and Conan.
scenario_41_dependencies() {
  local root="$WORK/41-dependencies"
  mkdir -p "$root/src" "$root/lib/include"
  printf '#pragma once\ninline int v() { return 1; }\n' >"$root/lib/include/demo.h"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_deps", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.32.10", target = "SDL2::SDL2" },
      mylib = { path = "../lib", target = "demo_lib" }
    },
    conan = { fmt = "10.2.1" }
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"

  local out flat
  if ! out="$(forge_in "$root" project dependencies 2>&1)"; then
    fail "project dependencies"
    return
  fi
  pass "project dependencies"
  flat="$(flatten <<<"$out")"

  if grep -qF "release-2.32.10" <<<"$flat"; then
    pass "git dependency listed with its tag"
  else
    fail "git dependency listed with its tag"
  fi
  if grep -qF "path:../lib" <<<"$flat"; then
    pass "path dependency shows its source"
  else
    fail "path dependency shows its source"
  fi
  if grep -qF "SDL2::SDL2" <<<"$flat"; then
    pass "link target listed"
  else
    fail "link target listed"
  fi
  if grep -qF "conan" <<<"$flat" && grep -qF "10.2.1" <<<"$flat"; then
    pass "conan package listed"
  else
    fail "conan package listed"
  fi
}
