# Project target shape: static/shared libraries, header install rules and the
# CMake policy version.
scenario_19_target_linkage() {
  local base="$WORK/19-target-linkage"

  make_library() { # <dir> <name> <linkage> <install_headers> [extra_dependencies]
    local dir="$1" name="$2" linkage="$3" headers="$4" deps="${5:-}"
    mkdir -p "$dir/src" "$dir/include"
    cat >"$dir/forge.lua" <<LUA
return {
  project = {
    name = "$name", type = "library", standard = "20",
    linkage = "$linkage", install_headers = $headers,
    cmake_policy_version = "3.5"
  },
  dependencies = { direct = { $deps }, conan = {} },
  resources = { files = {} }, scripts = {}, features = {}
}
LUA
    printf 'namespace demo { int answer() { return 42; } }\n' >"$dir/src/lib.cpp"
    printf '#pragma once\nnamespace demo { int answer(); }\n' >"$dir/include/demo.hpp"
  }

  # A dependency built here but not installed: it must be kept out of the
  # library's installed interface ($<BUILD_INTERFACE:...>) while still being
  # linked in the build tree.
  local dep="$base/dep"
  mkdir -p "$dep/include"
  cat >"$dep/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(netlib LANGUAGES CXX)
add_library(netlib INTERFACE)
target_include_directories(netlib INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int netlib_value() { return 42; }\n' >"$dep/include/net.h"

  # Static library with installed headers and a build-only dependency.
  local s="$base/static"
  make_library "$s" lib_static static true 'netlib = { path = "../dep" }'
  printf '#include <net.h>\nnamespace demo { int answer() { return netlib_value(); } }\n' >"$s/src/lib.cpp"
  if forge_in "$s" build >/dev/null 2>&1; then
    pass "static library builds"
  else
    fail "static library builds"
  fi
  assert_contains "$s/.config/cmake/CMakeLists.txt" "add_library(lib_static STATIC" \
    "static linkage emitted"
  assert_contains "$s/.config/cmake/CMakeLists.txt" \
    'target_link_libraries(lib_static PRIVATE $<BUILD_INTERFACE:netlib>)' \
    "a build-only dependency is kept out of the installed interface"
  assert_contains "$s/.config/cmake/CMakeLists.txt" "install(DIRECTORY" \
    "install_headers emits install rules"
  assert_contains "$s/CMakeLists.txt" "set(CMAKE_POLICY_VERSION_MINIMUM 3.5)" \
    "cmake_policy_version emitted"

  # Shared library without header install rules.
  local sh="$base/shared"
  make_library "$sh" lib_shared shared false
  forge_in "$sh" build >/dev/null 2>&1 || true
  assert_contains "$sh/.config/cmake/CMakeLists.txt" "add_library(lib_shared SHARED" \
    "shared linkage emitted"
  assert_lacks "$sh/.config/cmake/CMakeLists.txt" "install(DIRECTORY" \
    "install_headers = false omits install rules"

  # The built library is usable from a consumer (proves the target is real).
  local app="$base/app"
  mkdir -p "$app/src"
  cat >"$app/forge.lua" <<'LUA'
return {
  project = { name = "lib_consumer", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      demo = { path = "../static", target = "lib_static" }
    },
    conan = {}
  },
  resources = { files = {} }, scripts = {}, features = {}
}
LUA
  printf '#include <demo.hpp>\n#include <cstdio>\nint main() { std::printf("answer=%%d\\n", demo::answer()); return 0; }\n' \
    >"$app/src/main.cpp"
  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "consumer links the static library"
    return
  fi
  pass "consumer links the static library"
  assert_runs "$app/build/lib_consumer" "answer=42" "linked library returns its value"
}
