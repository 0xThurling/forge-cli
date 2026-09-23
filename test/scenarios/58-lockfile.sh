# forge.lock: git refs are pinned to commits, builds use the lock, and
# `forge install --update` moves them on.
scenario_58_lockfile() {
  local root="$WORK/58-lockfile"
  local repo="$root/repo"
  local app="$root/app"

  # Two commits, tagged v1 and v2, so the resolved SHA differs per tag.
  mkdir -p "$repo/include"
  cat >"$repo/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(locked LANGUAGES CXX)
add_library(locked INTERFACE)
target_include_directories(locked INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int locked() { return 1; }\n' >"$repo/include/locked.h"
  git -C "$repo" init -q
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@forge -c user.name=e2e commit -qm first
  git -C "$repo" tag v1
  printf '#pragma once\ninline int locked() { return 2; }\n' >"$repo/include/locked.h"
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@forge -c user.name=e2e commit -qm second
  git -C "$repo" tag v2
  local commit_v1 commit_v2
  commit_v1="$(git -C "$repo" rev-parse v1)"
  commit_v2="$(git -C "$repo" rev-parse v2)"

  mkdir -p "$app/src" "$app/local/include"
  printf '#pragma once\ninline int local() { return 0; }\n' >"$app/local/include/local.h"
  cat >"$app/local/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(localdep LANGUAGES CXX)
add_library(localdep INTERFACE)
target_include_directories(localdep INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  cat >"$app/forge.lua" <<LUA
return {
  project = { name = "demo_lock", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      locked = { git = "file://$repo", tag = "v1", target = "locked" },
      localdep = { path = "local", target = "localdep" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <locked.h>\n#include <local.h>\nint main() { return locked() + local(); }\n' \
    >"$app/src/main.cpp"

  # install resolves and records the commit.
  if ! forge_in "$app" install >/dev/null 2>&1; then
    fail "forge install writes the lock"
    return
  fi
  pass "forge install writes the lock"
  assert_exists "$app/forge.lock" "forge.lock created"
  assert_contains "$app/forge.lock" "$commit_v1" "the tag is pinned to its commit"
  assert_lacks "$app/forge.lock" '"localdep"' "local path dependencies are not locked"

  # The build fetches the pinned commit.
  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "builds from the lock"
    return
  fi
  pass "builds from the lock"
  assert_contains "$app/.config/cmake/CMakeLists.txt" "GIT_TAG \"$commit_v1\"" \
    "the generated CMake uses the locked commit"

  # A tag that moves upstream does not move the build: the lock pins the commit.
  git -C "$repo" tag -f v1 "$commit_v2" >/dev/null 2>&1
  forge_in "$app" build >/dev/null 2>&1 || true
  assert_contains "$app/.config/cmake/CMakeLists.txt" "GIT_TAG \"$commit_v1\"" \
    "a moved tag does not move the pinned commit"

  # Editing the manifest is a deliberate change, so the next install resolves it.
  sed_in_place 's/tag = "v1"/tag = "v2"/' "$app/forge.lua"
  forge_in "$app" install >/dev/null 2>&1 || true
  assert_contains "$app/forge.lock" "$commit_v2" "re-installing re-resolves the new ref"
  forge_in "$app" build >/dev/null 2>&1 || true
  assert_contains "$app/.config/cmake/CMakeLists.txt" "GIT_TAG \"$commit_v2\"" \
    "the build follows the refreshed lock"

  # --update re-resolves even an unchanged ref.
  local out
  out="$(forge_in "$app" install --update 2>&1 || true)"
  if grep -qE "Locked [0-9]+ dependenc" <<<"$(flatten <<<"$out")"; then
    pass "install --update reports what it locked"
  else
    fail "install --update reports what it locked"
  fi

  # Removing the dependency drops its entry.
  sed_in_place 's|locked = { git = "file://'"$repo"'", tag = "v2", target = "locked" },||' "$app/forge.lua"
  forge_in "$app" install >/dev/null 2>&1 || true
  assert_lacks "$app/forge.lock" "$commit_v2" "a removed dependency is unlocked"
}
