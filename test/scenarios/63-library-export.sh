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
  assert_contains "$cmake" "install(EXPORT demo_exportTargets" "export rules emitted"
  assert_contains "$cmake" "NAMESPACE demo_export::" "export namespace set"

  # Install it, then consume it with plain CMake.
  if ! forge_in "$root" install --prefix out >/dev/null 2>&1; then
    fail "forge install --prefix"
    return
  fi
  pass "forge install --prefix"
  assert_exists "$root/out/lib/cmake/demo_export/demo_exportConfig.cmake" \
    "a CMake package file is installed"
  assert_exists "$root/out/lib/cmake/demo_export/demo_exportTargets.cmake" \
    "the targets file is installed"
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

  # --- a dependency that travels with the installed package -----------------
  # `export` names the package to find and the target it provides, so the
  # installed interface can reference the dependency instead of dropping it.
  local dep="$root/dep"
  mkdir -p "$dep/src" "$dep/include"
  cat >"$dep/forge.lua" <<'LUA'
return {
  project = { name = "demo_dep", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} }, scripts = {}, features = {}
}
LUA
  printf '#pragma once\nnamespace dep { int value(); }\n' >"$dep/include/dep.hpp"
  printf '#include <dep.hpp>\nnamespace dep { int value() { return 40; } }\n' >"$dep/src/dep.cpp"

  local parent="$root/parent"
  mkdir -p "$parent/src" "$parent/include"
  cat >"$parent/forge.lua" <<'LUA'
return {
  project = { name = "demo_parent", type = "library", standard = "20", install_headers = true },
  dependencies = {
    direct = {
      demo_dep = { path = "../dep", target = "demo_dep",
                   export = { package = "demo_dep", target = "demo_dep::demo_dep" } }
    },
    conan = {}
  },
  resources = { files = {} }, scripts = {}, features = {}
}
LUA
  printf '#pragma once\nnamespace parent { int answer(); }\n' >"$parent/include/parent.hpp"
  printf '#include <parent.hpp>\n#include <dep.hpp>\nnamespace parent { int answer() { return dep::value() + 2; } }\n' \
    >"$parent/src/parent.cpp"

  if forge_in "$dep" build >/dev/null 2>&1 && forge_in "$dep" install --prefix "$root/out" >/dev/null 2>&1; then
    pass "an installable dependency builds and installs"
  else
    fail "an installable dependency builds and installs"
  fi
  if forge_in "$parent" build >/dev/null 2>&1 && forge_in "$parent" install --prefix "$root/out" >/dev/null 2>&1; then
    pass "a library with an exported dependency installs"
  else
    fail "a library with an exported dependency installs"
  fi
  assert_contains "$parent/.config/cmake/CMakeLists.txt" \
    'target_link_libraries(demo_parent PRIVATE $<BUILD_INTERFACE:demo_dep>$<INSTALL_INTERFACE:demo_dep::demo_dep>)' \
    "the installed interface links the imported target"
  assert_contains "$parent/.config/cmake/CMakeLists.txt" "find_dependency(demo_dep)" \
    "the generated package finds the dependency"
  assert_contains "$root/out/lib/cmake/demo_parent/demo_parentConfig.cmake" "find_dependency(demo_dep)" \
    "the installed package carries the find_dependency"
  assert_contains "$root/out/lib/cmake/demo_parent/demo_parentTargets.cmake" "demo_dep::demo_dep" \
    "the installed targets reference the imported target"

  # A consumer that finds only the parent still gets the dependency.
  local transitive="$root/transitive-consumer"
  mkdir -p "$transitive"
  cat >"$transitive/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(transitive_consumer LANGUAGES CXX)
find_package(demo_parent CONFIG REQUIRED)
add_executable(transitive_consumer main.cpp)
target_link_libraries(transitive_consumer PRIVATE demo_parent::demo_parent)
CMAKE
  printf '#include <parent.hpp>\nint main() { return parent::answer() == 42 ? 0 : 1; }\n' >"$transitive/main.cpp"
  if ! cmake -S "$transitive" -B "$transitive/build" -DCMAKE_PREFIX_PATH="$root/out" >/dev/null 2>&1; then
    fail "a consumer finds the parent package without finding the dependency"
    return
  fi
  pass "a consumer finds the parent package without finding the dependency"
  if cmake --build "$transitive/build" >/dev/null 2>&1 && "$transitive/build/transitive_consumer"; then
    pass "the dependency is linked transitively"
  else
    fail "the dependency is linked transitively"
  fi
}
