# Target defaults: a dependency without `target` links the dependency key, a
# project without sources becomes an INTERFACE library, and several channels
# link together.
scenario_46_target_defaults() {
  local base="$WORK/46-target-defaults"

  # A dependency declared without `target` links under its key name.
  local lib="$base/lib"
  mkdir -p "$lib/include"
  cat >"$lib/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(netlib LANGUAGES CXX)
add_library(netlib INTERFACE)
target_include_directories(netlib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int net_value() { return 5; }\n' >"$lib/include/net.h"

  local app="$base/app"
  mkdir -p "$app/src"
  cat >"$app/forge.lua" <<'LUA'
return {
  project = { name = "demo_defaults", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      netlib = { path = "../lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <net.h>\n#include <cstdio>\nint main() { std::printf("net=%%d\\n", net_value()); return 0; }\n' \
    >"$app/src/main.cpp"
  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "dependency without an explicit target builds"
    return
  fi
  pass "dependency without an explicit target builds"
  assert_contains "$app/.config/cmake/CMakeLists.txt" \
    "target_link_libraries(demo_defaults PRIVATE netlib)" \
    "the dependency key is linked"
  assert_runs "$app/build/demo_defaults" "net=5" "the linked library works"

  # A library with no sources becomes an INTERFACE target.
  local empty="$base/empty-lib"
  mkdir -p "$empty"
  cat >"$empty/forge.lua" <<'LUA'
return {
  project = { name = "demo_interface", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  if forge_in "$empty" build >/dev/null 2>&1; then
    pass "a source-less library builds"
  else
    fail "a source-less library builds"
  fi
  assert_contains "$empty/.config/cmake/CMakeLists.txt" "add_library(demo_interface INTERFACE)" \
    "INTERFACE target emitted for a source-less library"

  # Two channels in one project: both targets reach the link line.
  local mixed="$base/mixed"
  mkdir -p "$mixed/src" "$mixed/lib2/include"
  cat >"$mixed/lib2/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(lib2 LANGUAGES CXX)
add_library(lib2 INTERFACE)
target_include_directories(lib2 INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int two() { return 2; }\n' >"$mixed/lib2/include/two.h"
  cat >"$mixed/forge.lua" <<LUA
return {
  project = { name = "demo_mixed", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      first = { path = "lib2", target = "lib2" },
      second = { path = "$base/lib", target = "netlib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <two.h>\n#include <net.h>\nint main() { return two() + net_value(); }\n' \
    >"$mixed/src/main.cpp"
  if ! forge_in "$mixed" build >/dev/null 2>&1; then
    fail "two path dependencies build"
    return
  fi
  pass "two path dependencies build"
  local link
  link="$(grep -m1 'target_link_libraries' "$mixed/.config/cmake/CMakeLists.txt")"
  if [[ "$link" == *lib2* && "$link" == *netlib* ]]; then
    pass "both dependencies reach the link line"
  else
    fail "both dependencies reach the link line (got '$link')"
  fi
}
