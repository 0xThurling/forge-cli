# A dependency's tests and packaging stop at the dependency: a consumer neither
# builds another project's tests/news framework fetch, nor inherits its CPack
# configuration.
scenario_77_dependency_isolation() {
  local base="$WORK/77-dependency-isolation"
  local lib="$base/lib"
  local app="$base/app"

  # --- a versioned library with tests ---------------------------------------
  mkdir -p "$lib/src" "$lib/test"
  cat >"$lib/forge.lua" <<'LUA'
return {
  project = { name = "dep_lib", type = "library", standard = "20", version = "1.0.0" },
  dependencies = {
    direct = {
      googletest = { git = "https://github.com/google/googletest.git", tag = "v1.14.0" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = true
}
LUA
  printf 'int dep_value() { return 21; }\n' >"$lib/src/lib.cpp"
  printf '#include <gtest/gtest.h>\nTEST(Dep, Works) { EXPECT_EQ(1, 1); }\n' >"$lib/test/lib_test.cpp"

  # A stub cmake keeps this offline: only the generated files matter here.
  local bin="$base/bin"
  mkdir -p "$bin"
  printf '#!/usr/bin/env bash\nexit 0\n' >"$bin/cmake"
  chmod +x "$bin/cmake"
  local old_path="$PATH"
  export PATH="$bin:$PATH"
  forge_in "$lib" build >/dev/null 2>&1 || true
  export PATH="$old_path"

  local lib_cmake="$lib/.config/cmake/CMakeLists.txt"
  assert_contains "$lib_cmake" "if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR OR FORGE_BUILD_DEPENDENCY_TESTS)" \
    "the dependency guards its tests"
  assert_contains "$lib_cmake" "add_executable(\${PROJECT_NAME}_tests" \
    "the dependency still defines its test target"
  assert_order "$(flatten <"$lib_cmake")" "the test framework is fetched inside the guard" \
    "OR FORGE_BUILD_DEPENDENCY_TESTS)" "FetchContent_Declare(googletest" "gtest_discover_tests"
  assert_order "$(flatten <"$lib_cmake")" "the packaging is guarded" \
    "# --- Packaging (CPack) ---" "if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)" "include(CPack)"

  # --- a consumer with a path dependency ------------------------------------
  mkdir -p "$app/src"
  cat >"$app/forge.lua" <<'LUA'
return {
  project = { name = "dep_app", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      dep_lib = { path = "../lib", target = "dep_lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint dep_value();\nint main() { std::printf("%%d\\n", dep_value() * 2); return 0; }\n' \
    >"$app/src/main.cpp"

  # The proof: this builds offline. Without the guard, the dependency's test
  # block would fetch GoogleTest here.
  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "a consumer of a tested dependency builds offline"
    return
  fi
  pass "a consumer of a tested dependency builds offline"
  assert_runs "$app/build/dep_app" "42" "the consumer links the dependency"

  if [[ -d "$app/build/_deps" ]] && ls "$app/build/_deps" 2>/dev/null | grep -q "googletest"; then
    fail "the dependency's test framework is not fetched by the consumer"
  else
    pass "the dependency's test framework is not fetched by the consumer"
  fi

  if [[ -f "$app/build/CPackConfig.cmake" ]]; then
    fail "the dependency's packaging does not leak into the consumer"
  else
    pass "the dependency's packaging does not leak into the consumer"
  fi

  # The dependency's tests are not registered with the consumer's ctest either.
  local tests
  tests="$(cd "$app/build" && ctest -N 2>&1 | sed 's/\x1b\[[0-9;]*m//g' || true)"
  if grep -qE "Total Tests: 0|No tests were found" <<<"$tests"; then
    pass "the dependency's tests are not registered with the consumer"
  else
    fail "the dependency's tests are not registered with the consumer (got '$(flatten <<<"$tests" | head -c 160)')"
  fi
}
