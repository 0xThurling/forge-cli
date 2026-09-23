# `forge outdated` compares declared tags with the newest tag in the repository;
# `forge vendor` copies fetched dependencies into the project for offline builds.
scenario_71_outdated_vendor() {
  local base="$WORK/71-outdated-vendor"
  local repo="$base/repo"
  local proj="$base/proj"
  mkdir -p "$repo" "$proj/src"

  # A dependency repository with two version tags (v1.10.0 must win over v1.2.0).
  git -C "$repo" init -q -b main
  printf '#pragma once\n#define MYLIB_VERSION 1\n' >"$repo/mylib.hpp"
  cat >"$repo/CMakeLists.txt" <<'CMAKE'
add_library(mylib INTERFACE)
target_include_directories(mylib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR})
CMAKE
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@test -c user.name=e2e commit -qm "one"
  git -C "$repo" tag v1.2.0
  printf '#pragma once\n#define MYLIB_VERSION 2\n' >"$repo/mylib.hpp"
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@test -c user.name=e2e commit -qm "two"
  git -C "$repo" tag v1.10.0

  cat >"$proj/forge.lua" <<LUA
return {
  project = { name = "demo_outdated", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      mylib = { git = "file://$repo", tag = "v1.2.0" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("hello\\n"); return 0; }\n' >"$proj/src/main.cpp"

  # --- outdated -------------------------------------------------------------
  local out flat
  out="$(forge_in "$proj" outdated 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if [[ "$flat" == *"mylib"* && "$flat" == *"v1.10.0"* ]]; then
    pass "the newer tag is reported"
  else
    fail "the newer tag is reported (got '$flat')"
  fi

  out="$(forge_in "$proj" outdated --json 2>&1 || true)"
  if [[ "$out" == *'"name":"mylib"'* && "$out" == *'"latest":"v1.10.0"'* ]]; then
    pass "the JSON report lists the newest tag"
  else
    fail "the JSON report lists the newest tag (got '$out')"
  fi

  sed_in_place 's/tag = "v1.2.0"/tag = "v1.10.0"/' "$proj/forge.lua"
  out="$(forge_in "$proj" outdated 2>&1 || true)"
  if grep -qF "up to date" <<<"$(flatten <<<"$out")"; then
    pass "an up-to-date dependency is reported as such"
  else
    fail "an up-to-date dependency is reported as such"
  fi

  local plain="$base/plain"
  mkdir -p "$plain/src"
  make_plain_project "$plain" demo_plain
  out="$(forge_in "$plain" outdated 2>&1 || true)"
  if grep -qF "No git dependencies to check" <<<"$(flatten <<<"$out")"; then
    pass "a project without git dependencies says so"
  else
    fail "a project without git dependencies says so"
  fi

  # --- vendor ---------------------------------------------------------------
  sed_in_place 's/tag = "v1.10.0"/tag = "v1.2.0"/' "$proj/forge.lua"
  forge_in "$proj" install >/dev/null 2>&1 || true
  assert_exists "$proj/forge.lock" "forge install writes a lockfile"
  assert_contains "$proj/forge.lock" "mylib" "the lockfile pins the dependency"

  if ! forge_in "$proj" build >/dev/null 2>&1; then
    fail "the dependency builds before vendoring"
    return
  fi
  pass "the dependency builds before vendoring"

  out="$(forge_in "$proj" vendor 2>&1 || true)"
  assert_exists "$proj/external/mylib/mylib.hpp" "the dependency is copied into external/"
  assert_contains "$proj/forge.lua" 'path = "external/mylib"' "the dependency becomes a path dependency"
  assert_lacks "$proj/forge.lua" "file://" "the git URL is dropped"
  assert_lacks "$proj/forge.lock" "mylib" "the lock entry is dropped"

  # The proof of offline operation: the repository is gone, so a build that
  # still used git would fail.
  rm -rf "$repo" "$proj/build"
  if forge_in "$proj" build >/dev/null 2>&1; then
    pass "the vendored project builds without the repository"
  else
    fail "the vendored project builds without the repository"
  fi

  out="$(forge_in "$plain" vendor 2>&1 || true)"
  if grep -qF "No git dependencies to vendor" <<<"$(flatten <<<"$out")"; then
    pass "vendoring without git dependencies says so"
  else
    fail "vendoring without git dependencies says so"
  fi
}
