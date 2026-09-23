# `forge test --json` as a CI gate, and `forge add` for the vcpkg and pkg-config
# channels.
scenario_90_json_and_channels() {
  local root="$WORK/90-json-and-channels/app"
  mkdir -p "$root/src" "$root/test"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_json", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = true
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"
  printf '#include <gtest/gtest.h>\nTEST(Json, Fails) { EXPECT_EQ(1, 2); }\n' >"$root/test/json_test.cpp"

  # --- a failing suite fails the command -----------------------------------
  local out
  if out="$(forge_in "$root" test --json 2>&1)"; then
    fail "a failing suite exits non-zero"
  else
    pass "a failing suite exits non-zero"
  fi
  local json_line
  json_line="$(tail -1 <<<"$(sed 's/\x1b\[[0-9;]*m//g' <<<"$out")")"
  if python3 -c "import json,sys; d=json.loads(sys.argv[1]); sys.exit(0 if d['passed'] is False and d['tests'] == 1 else 1)" \
    "$json_line" 2>/dev/null; then
    pass "the JSON summary reports the failure"
  else
    fail "the JSON summary reports the failure (got '${json_line:0:120}')"
  fi

  # --- a passing suite passes ----------------------------------------------
  printf '#include <gtest/gtest.h>\nTEST(Json, Passes) { EXPECT_EQ(1, 1); }\n' >"$root/test/json_test.cpp"
  if out="$(forge_in "$root" test --json 2>&1)"; then
    pass "a passing suite exits 0"
  else
    fail "a passing suite exits 0"
  fi
  json_line="$(tail -1 <<<"$(sed 's/\x1b\[[0-9;]*m//g' <<<"$out")")"
  if python3 -c "import json,sys; d=json.loads(sys.argv[1]); sys.exit(0 if d['passed'] is True and d['failures'] == 0 else 1)" \
    "$json_line" 2>/dev/null; then
    pass "the JSON summary reports success"
  else
    fail "the JSON summary reports success (got '${json_line:0:120}')"
  fi

  # --- the remaining channels can be added from the CLI ---------------------
  local chan="$WORK/90-json-and-channels/channels"
  mkdir -p "$chan/src" "$chan/bin"
  make_plain_project "$chan" demo_channels

  if ! forge_in "$chan" add fmt --vcpkg fmt::fmt >/dev/null 2>&1; then
    fail "forge add --vcpkg"
  else
    pass "forge add --vcpkg"
  fi
  assert_contains "$chan/forge.lua" 'fmt = "fmt::fmt"' "the vcpkg dependency is written"

  if ! forge_in "$chan" add zlib --pkg-config >/dev/null 2>&1; then
    fail "forge add --pkg-config"
  else
    pass "forge add --pkg-config"
  fi
  assert_contains "$chan/forge.lua" '"zlib"' "the pkg-config module is written"

  # The channels reach the generated CMake (a stub cmake keeps this offline:
  # the real build would need a vcpkg checkout).
  printf '#!/usr/bin/env bash\nexit 0\n' >"$chan/bin/cmake"
  chmod +x "$chan/bin/cmake"
  local old_path="$PATH"
  export PATH="$chan/bin:$PATH"
  forge_in "$chan" build >/dev/null 2>&1 || true
  export PATH="$old_path"

  local cmake="$chan/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "find_package(fmt REQUIRED)" "the vcpkg package is found"
  assert_contains "$cmake" "pkg_check_modules(ZLIB REQUIRED IMPORTED_TARGET zlib)" \
    "the pkg-config module is imported"

  # --- exactly one source --------------------------------------------------
  out="$(forge_in "$chan" add bad --vcpkg x --conan 1.0 2>&1 || true)"
  if grep -qF "choose exactly one source" <<<"$(flatten <<<"$out")"; then
    pass "two sources at once are rejected"
  else
    fail "two sources at once are rejected"
  fi
}
