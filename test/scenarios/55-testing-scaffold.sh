# `testing = true` scaffolds a test directory, injects googletest and emits the
# test target. cmake/ctest are stubbed so no toolchain or network is needed —
# which also pins the exact CMake invocation.
scenario_55_testing_scaffold() {
  local root="$WORK/55-testing-scaffold"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src"
  make_plain_project "$root" demo_testing

  # Stub cmake + ctest: log the arguments, succeed.
  for tool in cmake ctest; do
    cat >"$bin/$tool" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$0") $*" >>"${TOOL_LOG:-/dev/null}"
exit 0
STUB
    chmod +x "$bin/$tool"
  done

  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_testing", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = true
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"

  local old_path="$PATH"
  export PATH="$bin:$PATH"
  export TOOL_LOG="$root/tools.log"

  local out
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds with testing = true"
    tail -5 <<<"$out"
    export PATH="$old_path"
    return
  fi
  pass "builds with testing = true"

  assert_exists "$root/test/main.cpp" "test directory scaffolded"
  assert_contains "$root/test/main.cpp" "TEST(HelloTest, BasicAssertions)" \
    "a gtest smoke test is written"
  assert_contains "$root/forge.lua" "googletest" "googletest dependency injected"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "enable_testing()" "test section emitted"
  assert_contains "$cmake" "gtest_discover_tests" "test discovery emitted"
  assert_contains "$cmake" "target_link_libraries(\${PROJECT_NAME}_tests PUBLIC GTest::gtest_main)" \
    "test target links googletest"

  assert_contains "$root/tools.log" "cmake -B build" "cmake configured"
  assert_contains "$root/tools.log" "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON" "compile DB requested"
  assert_contains "$root/tools.log" "cmake --build build" "cmake built"

  export PATH="$old_path"
}
