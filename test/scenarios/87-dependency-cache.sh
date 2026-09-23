# The shared dependency cache: a fetched git dependency is cloned once, reused
# by later builds (and other projects), and makes an offline build possible.
scenario_87_dependency_cache() {
  local base="$WORK/87-dependency-cache"
  local repo="$base/repo"
  local proj="$base/proj"
  local cache="$base/cache"
  mkdir -p "$repo" "$proj/src"

  git -C "$repo" init -q -b main
  cat >"$repo/CMakeLists.txt" <<'CMAKE'
add_library(cachedlib INTERFACE)
target_include_directories(cachedlib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR})
CMAKE
  printf '#pragma once\n#define CACHED 1\n' >"$repo/cached.hpp"
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@test -c user.name=e2e commit -qm one
  git -C "$repo" tag v1.0.0

  cat >"$proj/forge.lua" <<LUA
return {
  project = { name = "demo_cache", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      cachedlib = { git = "file://$repo", tag = "v1.0.0", target = "cachedlib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("cached\\n"); return 0; }\n' >"$proj/src/main.cpp"

  # Isolate the cache, so the test never touches the user's (the runner sets a
  # workspace-local one that must be restored).
  local old_cache="${FORGE_CACHE_DIR:-}"
  export FORGE_CACHE_DIR="$cache"

  forge_in "$proj" install >/dev/null 2>&1 || true
  if ! forge_in "$proj" build >/dev/null 2>&1; then
    fail "the project builds with a cached dependency"
    export FORGE_CACHE_DIR="$old_cache"
    return
  fi
  pass "the project builds with a cached dependency"

  local entries
  entries="$(find "$cache" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | wc -l)"
  if [[ "$entries" == "1" ]]; then
    pass "the dependency is cached"
  else
    fail "the dependency is cached (found $entries entries)"
  fi
  assert_contains "$proj/.config/cmake/CMakeLists.txt" "FETCHCONTENT_SOURCE_DIR_CACHEDLIB" \
    "the cache is handed to CMake"

  # A branch reference is not cached: it moves, and a cached copy would freeze it.
  sed_in_place 's/tag = "v1.0.0"/tag = "main"/' "$proj/forge.lua"
  forge_in "$proj" install --update >/dev/null 2>&1 || true
  forge_in "$proj" build >/dev/null 2>&1 || true
  entries="$(find "$cache" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | wc -l)"
  if [[ "$entries" == "1" ]]; then
    pass "a branch reference is not cached"
  else
    fail "a branch reference is not cached (found $entries entries)"
  fi

  # Back to the tag, then the proof: the repository is gone, so only the cache
  # can serve the build.
  sed_in_place 's/tag = "main"/tag = "v1.0.0"/' "$proj/forge.lua"
  forge_in "$proj" install --update >/dev/null 2>&1 || true
  rm -rf "$repo" "$proj/build"
  if forge_in "$proj" build >/dev/null 2>&1; then
    pass "a cached dependency builds without the repository"
  else
    fail "a cached dependency builds without the repository"
  fi

  # --- cache list / clear ---------------------------------------------------
  local out
  out="$(forge cache list 2>&1 || true)"
  if grep -qF "cachedlib-" <<<"$(flatten <<<"$out")"; then
    pass "forge cache list shows the entry"
  else
    fail "forge cache list shows the entry (got '$(flatten <<<"$out" | head -c 120)')"
  fi

  forge cache clear >/dev/null 2>&1 || true
  entries="$(find "$cache" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | wc -l)"
  if [[ "$entries" == "0" ]]; then
    pass "forge cache clear empties the cache"
  else
    fail "forge cache clear empties the cache (found $entries entries)"
  fi

  # With the cache empty and the repository gone, the fetch has nowhere to go.
  rm -rf "$proj/build"
  if forge_in "$proj" build >/dev/null 2>&1; then
    fail "the build needs the cache (or the repository)"
  else
    pass "the build needs the cache (or the repository)"
  fi

  export FORGE_CACHE_DIR="$old_cache"
}
