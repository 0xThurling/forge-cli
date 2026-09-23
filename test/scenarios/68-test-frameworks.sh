# Test-framework choice (catch2, doctest) and the benchmark target.
scenario_68_test_frameworks() {
  local base="$WORK/68-test-frameworks"

  # --- catch2, resolved from a fake package ---------------------------------
  local catch="$base/catch2"
  local fake="$catch/fake-catch2"
  mkdir -p "$fake/lib/cmake/Catch2" "$catch/src" "$catch/test"
  printf 'int main() { return 0; }\n' >"$fake/main.cpp"
  g++ -c "$fake/main.cpp" -o "$fake/main.o"
  ar rcs "$fake/lib/libcatch2main.a" "$fake/main.o"
  cat >"$fake/lib/cmake/Catch2/Catch2Config.cmake" <<CMAKE
if(NOT TARGET Catch2::Catch2WithMain)
  add_library(Catch2::Catch2WithMain STATIC IMPORTED)
  set_target_properties(Catch2::Catch2WithMain PROPERTIES IMPORTED_LOCATION "$fake/lib/libcatch2main.a")
endif()
list(APPEND CMAKE_MODULE_PATH "\${CMAKE_CURRENT_LIST_DIR}")
CMAKE
  cat >"$fake/lib/cmake/Catch2/Catch.cmake" <<'CMAKE'
function(catch_discover_tests target)
  add_test(NAME ${target} COMMAND ${target})
endfunction()
CMAKE
  # find_package(Catch2 3) needs a version file alongside the config.
  cat >"$fake/lib/cmake/Catch2/Catch2ConfigVersion.cmake" <<'CMAKE'
set(PACKAGE_VERSION "3.5.0")
if(PACKAGE_VERSION VERSION_LESS PACKAGE_FIND_VERSION)
  set(PACKAGE_VERSION_COMPATIBLE FALSE)
else()
  set(PACKAGE_VERSION_COMPATIBLE TRUE)
  if(PACKAGE_FIND_VERSION STREQUAL PACKAGE_VERSION)
    set(PACKAGE_VERSION_EXACT TRUE)
  endif()
endif()
CMAKE

  cat >"$catch/forge.lua" <<LUA
return {
  project = { name = "demo_catch2", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = { framework = "catch2" },
  build = { cmake_prefix_path = { "$fake" } }
}
LUA
  printf 'int main() { return 0; }\n' >"$catch/src/main.cpp"
  printf '// a catch2 test file\n' >"$catch/test/main_test.cpp"

  if ! forge_in "$catch" build >/dev/null 2>&1; then
    fail "builds with the catch2 framework"
    return
  fi
  pass "builds with the catch2 framework"

  local cmake="$catch/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "find_package(Catch2 3 REQUIRED)" "catch2 is found"
  assert_contains "$cmake" "Catch2::Catch2WithMain" "the catch2 target is linked"
  assert_contains "$cmake" "catch_discover_tests(\${PROJECT_NAME}_tests)" "catch2 discovery emitted"
  assert_lacks "$cmake" "GTest::gtest_main" "gtest is not linked"

  local out
  out="$(forge_in "$catch" test 2>&1 || true)"
  if grep -qF "All tests passed" <<<"$(flatten <<<"$out")"; then
    pass "forge test runs the catch2 target"
  else
    fail "forge test runs the catch2 target"
  fi

  # --- doctest, wiring only (stub cmake keeps this offline) ------------------
  local doctest="$base/doctest"
  local bin="$doctest/bin"
  mkdir -p "$bin" "$doctest/src" "$doctest/test"
  make_plain_project "$doctest" demo_doctest
  cat >"$bin/cmake" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
  chmod +x "$bin/cmake"
  cat >"$doctest/forge.lua" <<'LUA'
return {
  project = { name = "demo_doctest", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = { framework = "doctest" }
}
LUA
  printf 'int main() { return 0; }\n' >"$doctest/src/main.cpp"
  printf '// a doctest test file\n' >"$doctest/test/main_test.cpp"
  export PATH="$bin:$PATH"
  forge_in "$doctest" build >/dev/null 2>&1 || true
  local old_path="$PATH"
  export PATH="$old_path"
  cmake="$doctest/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "doctest_with_main" "doctest target linked"
  assert_contains "$cmake" "FetchContent_Declare(doctest" "doctest fetched"
  assert_contains "$cmake" "add_test(NAME \${PROJECT_NAME}_tests" "doctest registered with ctest"

  # --- benchmarks ------------------------------------------------------------
  local bench="$base/bench"
  local bbin="$bench/bin"
  mkdir -p "$bbin" "$bench/src" "$bench/bench"
  make_plain_project "$bench" demo_bench
  cat >"$bbin/cmake" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
  chmod +x "$bbin/cmake"
  cat >"$bench/forge.lua" <<'LUA'
return {
  project = { name = "demo_bench", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = { benchmark = true }
}
LUA
  printf 'int main() { return 0; }\n' >"$bench/src/main.cpp"
  printf '// google benchmark source\n' >"$bench/bench/bench.cpp"

  export PATH="$bbin:$PATH"
  forge_in "$bench" build >/dev/null 2>&1 || true
  export PATH="$old_path"

  cmake="$bench/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "FetchContent_Declare(googlebenchmark" "google benchmark fetched"
  assert_contains "$cmake" "add_executable(\${PROJECT_NAME}_bench" "bench target emitted"
  assert_contains "$cmake" "benchmark::benchmark" "bench target linked"

  # forge bench runs the built binary (the stub cmake leaves ours in place).
  mkdir -p "$bench/build"
  cat >"$bench/build/demo_bench_bench" <<'STUB'
#!/usr/bin/env bash
echo "benchmark-ran $*"
STUB
  chmod +x "$bench/build/demo_bench_bench"
  out="$(forge_in "$bench" bench --benchmark_filter=all 2>&1 || true)"
  if [[ "$out" == *"benchmark-ran --benchmark_filter=all"* ]]; then
    pass "forge bench runs the binary with pass-through arguments"
  else
    fail "forge bench runs the binary with pass-through arguments (got '$(flatten <<<"$out")')"
  fi

  # Without benchmark = true it explains what to enable.
  assert_exit 1 "forge bench without benchmarks fails" forge_in "$catch" bench
  out="$(forge_in "$catch" bench 2>&1 || true)"
  if grep -qF "benchmarks are not enabled" <<<"$(flatten <<<"$out")"; then
    pass "the missing benchmark setup is explained"
  else
    fail "the missing benchmark setup is explained"
  fi
}
