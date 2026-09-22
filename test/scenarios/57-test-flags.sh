# `forge test` forwards the build flags and hands the filter to ctest.
# cmake/ctest are stubbed, so this pins the exact invocations.
scenario_57_test_flags() {
  local root="$WORK/57-test-flags"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src"
  make_plain_project "$root" demo_testflags

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
  project = { name = "demo_testflags", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { presets = { "warnings" } },
  testing = true
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"
  mkdir -p "$root/test"
  printf '#include <gtest/gtest.h>\nTEST(X, Y) { EXPECT_TRUE(true); }\n' >"$root/test/x_test.cpp"

  local old_path="$PATH"
  export PATH="$bin:$PATH"
  export TOOL_LOG="$root/tools.log"

  # --release and --standard reach the CMake configure step.
  if ! forge_in "$root" test --release --standard 17 >/dev/null 2>&1; then
    fail "forge test with flags"
    export PATH="$old_path"
    return
  fi
  pass "forge test with flags"
  assert_contains "$root/tools.log" "-DCMAKE_BUILD_TYPE=Release" "--release forwarded"
  assert_contains "$root/.config/cmake/CMakeLists.txt" "set(CMAKE_CXX_STANDARD 17)" \
    "--standard forwarded"

  # The filter reaches ctest; without one, no -R is passed.
  : >"$root/tools.log"
  forge_in "$root" test --filter 'MySuite.*' >/dev/null 2>&1 || true
  assert_contains "$root/tools.log" "ctest --test-dir build --output-on-failure -R MySuite.*" \
    "the filter is handed to ctest"

  : >"$root/tools.log"
  forge_in "$root" test >/dev/null 2>&1 || true
  assert_lacks "$root/tools.log" " -R " "no filter means no -R"

  # --no-config-presets drops the configured presets for the test build too.
  forge_in "$root" test --no-config-presets >/dev/null 2>&1 || true
  assert_lacks "$root/.config/cmake/CMakeLists.txt" "CXX_COMPILER_ID:GNU,Clang,AppleClang>:-Wall" \
    "--no-config-presets applies to forge test"

  export PATH="$old_path"
}
