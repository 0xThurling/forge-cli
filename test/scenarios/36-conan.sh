# The Conan channel, end to end, with a stub `conan` that mimics Conan's
# CMakeDeps + CMakeToolchain output:
#   * the conanfile Forge generates
#   * the exact `conan install` invocation
#   * Forge's parsing of find_package / target_link_libraries lines
#   * the conan toolchain being passed to CMake and actually read
#   * a failing conan, and a missing conan binary
#   * `forge install --prefix` installing a built library
scenario_36_conan() {
  local root="$WORK/36-conan"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src"

  # --- stub conan -----------------------------------------------------------
  cat >"$bin/conan" <<'STUB'
#!/usr/bin/env bash
# Mimics the parts of Conan that Forge depends on: a CMakeToolchain file, a
# CMakeDeps config per package, and the stdout/stderr lines Forge parses.
echo "conan $*" >>"${CONAN_STUB_LOG:-/dev/null}"

# Honor `-s build_type=`, like Conan's cmake_layout does.
build_type="Release"
for arg in "$@"; do
  case "$arg" in
    build_type=*) build_type="${arg#build_type=}" ;;
  esac
done
out="build/build/$build_type/generators"
mkdir -p "$out"
cat >"$out/conan_toolchain.cmake" <<CMAKE
set(CONAN_STUB_TOOLCHAIN ON CACHE BOOL "stub toolchain marker" FORCE)
list(APPEND CMAKE_PREFIX_PATH "\${CMAKE_CURRENT_LIST_DIR}")
CMAKE

for pkg in ${CONAN_STUB_PACKAGES:-fmt spdlog}; do
  mkdir -p "$out/$pkg"
  cat >"$out/$pkg/$pkg-config.cmake" <<CMAKE
if(NOT TARGET $pkg::$pkg)
  add_library($pkg::$pkg INTERFACE IMPORTED)
endif()
CMAKE
  echo "find_package($pkg)" >&2
  echo "target_link_libraries($pkg::$pkg)" >&2
done

exit "${CONAN_STUB_EXIT:-0}"
STUB
  chmod +x "$bin/conan"

  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_conan", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = { fmt = "10.2.1", spdlog = "1.12.0" } },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("conan-ok\\n"); return 0; }\n' \
    >"$root/src/main.cpp"

  local old_path="$PATH"
  export PATH="$bin:$PATH"
  export CONAN_STUB_LOG="$root/conan-calls.log"
  export CONAN_STUB_PACKAGES="fmt spdlog"
  export CONAN_STUB_EXIT=0

  # --- install --------------------------------------------------------------
  if forge_in "$root" install >/dev/null 2>&1; then
    pass "forge install runs the stub conan"
  else
    fail "forge install runs the stub conan"
    export PATH="$old_path"
    return
  fi

  assert_contains "$root/conan-calls.log" \
    "install .config/conanfile.txt --output-folder=build --build=missing -s build_type=Release" \
    "conan is invoked with the expected arguments"

  local conanfile="$root/.config/conanfile.txt"
  assert_contains "$conanfile" "[requires]" "conanfile has a requires section"
  assert_contains "$conanfile" "fmt/10.2.1" "first package recorded"
  assert_contains "$conanfile" "spdlog/1.12.0" "second package recorded"
  assert_contains "$conanfile" "CMakeDeps" "CMakeDeps generator requested"
  assert_contains "$conanfile" "CMakeToolchain" "CMakeToolchain generator requested"
  assert_contains "$conanfile" "cmake_layout" "cmake_layout requested"

  # --- build ----------------------------------------------------------------
  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with Conan dependencies"
    export PATH="$old_path"
    return
  fi
  pass "builds with Conan dependencies"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "find_package(fmt REQUIRED)" "find_package parsed from conan output"
  assert_contains "$cmake" "find_package(spdlog REQUIRED)" "second find_package parsed"
  assert_contains "$cmake" "target_link_libraries(demo_conan PRIVATE fmt::fmt spdlog::spdlog)" \
    "target_link_libraries parsed from conan output"
  assert_contains "$root/build/CMakeCache.txt" "CMAKE_TOOLCHAIN_FILE" \
    "conan toolchain passed to CMake"
  assert_contains "$root/build/CMakeCache.txt" "CONAN_STUB_TOOLCHAIN:BOOL=ON" \
    "conan toolchain actually read by CMake"
  assert_runs "$root/build/demo_conan" "conan-ok" "binary links against the conan targets"

  # Nothing changed, so the next build must not pay for `conan install` again.
  local calls_before calls_after
  calls_before="$(wc -l <"$root/conan-calls.log")"
  if forge_in "$root" build >/dev/null 2>&1; then
    calls_after="$(wc -l <"$root/conan-calls.log")"
    if [[ "$calls_after" -eq "$calls_before" ]]; then
      pass "an unchanged build does not run conan again"
    else
      fail "an unchanged build does not run conan again ($calls_before -> $calls_after)"
    fi
  else
    fail "an unchanged build does not run conan again"
  fi

  # A debug build installs Conan for Debug, so the toolchain the configure step
  # looks for is the one Conan wrote (build/build/Debug/generators).
  if ! forge_in "$root" build --debug >/dev/null 2>&1; then
    fail "a debug build installs conan for Debug"
  else
    pass "a debug build installs conan for Debug"
  fi
  assert_contains "$root/conan-calls.log" "-s build_type=Debug" \
    "the build type is forwarded to conan"
  assert_contains "$root/build/CMakeCache.txt" "build/build/Debug/generators/conan_toolchain.cmake" \
    "the Debug toolchain is the one passed to CMake"

  # --- a failing conan ------------------------------------------------------
  export CONAN_STUB_EXIT=1
  local out flat
  out="$(forge_in "$root" install 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  assert_exit 1 "a failing conan fails the install" forge_in "$root" install
  if grep -qF "find_package" <<<"$flat"; then
    pass "conan stderr is surfaced"
  else
    fail "conan stderr is surfaced"
  fi
  export CONAN_STUB_EXIT=0

  # --- install --prefix -----------------------------------------------------
  local lib="$root/libdemo"
  mkdir -p "$lib/src" "$lib/include"
  cat >"$lib/forge.lua" <<'LUA'
return {
  project = { name = "demo_install", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'namespace demo { int answer() { return 42; } }\n' >"$lib/src/lib.cpp"
  printf '#pragma once\nnamespace demo { int answer(); }\n' >"$lib/include/demo.hpp"
  forge_in "$lib" build >/dev/null 2>&1 || true

  if forge_in "$lib" install --prefix out >/dev/null 2>&1; then
    pass "install --prefix"
  else
    fail "install --prefix"
  fi
  assert_exists "$lib/out/lib/libdemo_install.a" "library installed to the prefix"
  assert_exists "$lib/out/include/demo.hpp" "headers installed to the prefix"

  # Without a build directory the install reports and fails.
  local fresh="$root/fresh"
  mkdir -p "$fresh/src"
  make_plain_project "$fresh" demo_fresh
  assert_exit 1 "install --prefix without a build fails" \
    forge_in "$fresh" install --prefix out

  export PATH="$old_path"
}
