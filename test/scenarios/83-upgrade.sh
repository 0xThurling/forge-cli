# `forge upgrade`: report which git dependencies can be bumped, and (with
# --apply) write the new tags and re-pin forge.lock.
scenario_83_upgrade() {
  local base="$WORK/83-upgrade"
  local repo="$base/repo"
  local proj="$base/proj"
  mkdir -p "$repo" "$proj/src"

  # A dependency repository with two version tags.
  git -C "$repo" init -q -b main
  printf '#pragma once\n#define LIB_VERSION 1\n' >"$repo/lib.hpp"
  cat >"$repo/CMakeLists.txt" <<'CMAKE'
add_library(thelib INTERFACE)
target_include_directories(thelib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR})
CMAKE
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@test -c user.name=e2e commit -qm one
  git -C "$repo" tag v1.0.0
  printf '#pragma once\n#define LIB_VERSION 2\n' >"$repo/lib.hpp"
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@test -c user.name=e2e commit -qm two
  git -C "$repo" tag v2.0.0

  cat >"$proj/forge.lua" <<LUA
return {
  project = { name = "demo_upgrade", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      thelib = { git = "file://$repo", tag = "v1.0.0", target = "thelib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("hello\\n"); return 0; }\n' >"$proj/src/main.cpp"

  # --- the report -----------------------------------------------------------
  local out flat
  out="$(forge_in "$proj" upgrade 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if [[ "$flat" == *"thelib"* && "$flat" == *"v2.0.0"* ]]; then
    pass "the newer tag is reported"
  else
    fail "the newer tag is reported (got '${flat:0:160}')"
  fi
  assert_contains "$proj/forge.lua" 'tag = "v1.0.0"' "the report does not touch forge.lua"

  out="$(forge_in "$proj" upgrade --json 2>&1 || true)"
  if [[ "$out" == *'"name":"thelib"'* && "$out" == *'"latest":"v2.0.0"'* ]]; then
    pass "the JSON report lists the bump"
  else
    fail "the JSON report lists the bump (got '${out:0:160}')"
  fi

  # --- unknown dependency ---------------------------------------------------
  assert_exit 1 "an unknown dependency is rejected" forge_in "$proj" upgrade nope
  out="$(forge_in "$proj" upgrade nope 2>&1 || true)"
  if grep -qF "not a git dependency" <<<"$(flatten <<<"$out")"; then
    pass "the unknown dependency is named"
  else
    fail "the unknown dependency is named"
  fi

  # --- applying -------------------------------------------------------------
  if ! forge_in "$proj" upgrade --apply >/dev/null 2>&1; then
    fail "forge upgrade --apply"
    return
  fi
  pass "forge upgrade --apply"
  assert_contains "$proj/forge.lua" 'tag = "v2.0.0"' "the tag is written to forge.lua"
  assert_exists "$proj/forge.lock" "the lock is re-pinned"

  # The upgraded dependency still builds (and the lock pins the new commit).
  if ! forge_in "$proj" build >/dev/null 2>&1; then
    fail "the project builds after the upgrade"
  else
    pass "the project builds after the upgrade"
  fi

  # --- nothing left to do ---------------------------------------------------
  out="$(forge_in "$proj" upgrade 2>&1 || true)"
  if grep -qF "up to date" <<<"$(flatten <<<"$out")"; then
    pass "an upgraded project reports up to date"
  else
    fail "an upgraded project reports up to date"
  fi
}
