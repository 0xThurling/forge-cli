# The build context: every contribution a Lua build script makes belongs to
# exactly one build, so a second build in the same process produces exactly the
# same generated CMake instead of accumulating state.
scenario_75_build_context() {
  local root="$WORK/75-build-context"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_context

  cat >"$root/.config/forge/build/contrib.lua" <<'LUA'
forge.add_cmake("set(FROM_SNIPPET 1)")
forge.add_section("contrib", "after:project_target", "set(CONTRIB_SECTION 1)")
return {
  cmakeOptions = {
    variables = { FROM_OPTIONS = "1" },
    definitions = { "FROM_SCRIPT=1" },
    findPackages = { "Threads" }
  }
}
LUA
  printf '#include <cstdio>\nint main() { std::printf("%%d\\n", FROM_SCRIPT); return 0; }\n' \
    >"$root/src/main.cpp"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "the project builds"
    return
  fi
  pass "the project builds"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "set(FROM_SNIPPET 1)" "add_cmake reaches the generated file"
  assert_contains "$cmake" "set(CONTRIB_SECTION 1)" "add_section reaches the generated file"
  assert_contains "$cmake" "set(FROM_OPTIONS \"1\")" "cmakeOptions variables reach the file"
  assert_contains "$cmake" "add_compile_definitions(FROM_SCRIPT=1)" "cmakeOptions definitions reach the file"
  assert_contains "$cmake" "find_package(Threads REQUIRED)" "cmakeOptions packages reach the file"
  assert_runs "$root/build/demo_context" "1" "the contributions reach the binary"

  local occurrences
  occurrences="$(grep -c "set(CONTRIB_SECTION 1)" "$cmake" || true)"
  if [[ "$occurrences" == "1" ]]; then
    pass "the section is emitted exactly once"
  else
    fail "the section is emitted exactly once (found $occurrences)"
  fi

  # Rebuilding must regenerate exactly the same file: contributions belong to a
  # build, not to the process.
  cp "$cmake" "$root/first-build.cmake"
  forge_in "$root" build >/dev/null 2>&1 || true
  if diff -q "$root/first-build.cmake" "$cmake" >/dev/null 2>&1; then
    pass "rebuilding regenerates the same file"
  else
    fail "rebuilding regenerates the same file"
  fi

  # `forge test` builds a second time in the same process (and adds the test
  # framework). The Lua contributions must still appear exactly once.
  local bin="$root/bin"
  mkdir -p "$bin"
  printf '#!/usr/bin/env bash\nexit 0\n' >"$bin/cmake"
  printf '#!/usr/bin/env bash\nexit 0\n' >"$bin/ctest"
  chmod +x "$bin/cmake" "$bin/ctest"
  local old_path="$PATH"
  export PATH="$bin:$PATH"
  forge_in "$root" test >/dev/null 2>&1 || true
  export PATH="$old_path"

  occurrences="$(grep -c "set(CONTRIB_SECTION 1)" "$cmake" || true)"
  if [[ "$occurrences" == "1" ]]; then
    pass "a second build in the same process does not duplicate contributions"
  else
    fail "a second build in the same process does not duplicate contributions (found $occurrences)"
  fi
  occurrences="$(grep -c "set(FROM_SNIPPET 1)" "$cmake" || true)"
  if [[ "$occurrences" == "1" ]]; then
    pass "add_cmake snippets are not duplicated either"
  else
    fail "add_cmake snippets are not duplicated either (found $occurrences)"
  fi
}
