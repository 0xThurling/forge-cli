# Library packaging: version, SOVERSION, an exportable CMake package, and a
# real consumer that finds it with find_package().
scenario_63_library_export() {
  local root="$WORK/63-library-export"
  mkdir -p "$root/src" "$root/include"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_export", type = "library", standard = "20", version = "1.2.3", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#pragma once\nnamespace demo { int answer(); }\n' >"$root/include/demo.hpp"
  printf 'namespace demo { int answer() { return 42; } }\n' >"$root/src/lib.cpp"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "a versioned library builds"
    return
  fi
  pass "a versioned library builds"

  assert_contains "$root/CMakeLists.txt" "project(demo_export VERSION 1.2.3 LANGUAGES CXX C)" \
    "project version emitted"
  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "set_target_properties(demo_export PROPERTIES VERSION 1.2.3 SOVERSION 1)" \
    "version and soversion set"
  assert_contains "$cmake" "BUILD_INTERFACE" "build-tree include path separated"
  assert_contains "$cmake" "INSTALL_INTERFACE" "install-tree include path separated"
  assert_contains "$cmake" "install(EXPORT demo_exportConfig" "export rules emitted"
  assert_contains "$cmake" "NAMESPACE demo_export::" "export namespace set"

  # Install it, then consume it with plain CMake.
  if ! forge_in "$root" install --prefix out >/dev/null 2>&1; then
    fail "forge install --prefix"
    return
  fi
  pass "forge install --prefix"
  assert_exists "$root/out/lib/cmake/demo_export/demo_exportConfig.cmake" \
    "a CMake package file is installed"
  assert_exists "$root/out/include/demo.hpp" "headers are installed"

  local consumer="$root/consumer"
  mkdir -p "$consumer"
  cat >"$consumer/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(consumer LANGUAGES CXX)
find_package(demo_export CONFIG REQUIRED)
add_executable(consumer main.cpp)
target_link_libraries(consumer PRIVATE demo_export::demo_export)
CMAKE
  printf '#include <demo.hpp>\nint main() { return demo::answer() == 42 ? 0 : 1; }\n' >"$consumer/main.cpp"

  if ! cmake -S "$consumer" -B "$consumer/build" -DCMAKE_PREFIX_PATH="$root/out" >/dev/null 2>&1; then
    fail "a consumer configures with find_package"
    return
  fi
  pass "a consumer configures with find_package"
  if cmake --build "$consumer/build" >/dev/null 2>&1 && "$consumer/build/consumer"; then
    pass "the consumer links and runs against the exported target"
  else
    fail "the consumer links and runs against the exported target"
  fi
}
