# Flag presets reach the generated CMake, and raw flags are passed through.
scenario_28_presets() {
  local root="$WORK/28-presets"
  make_plain_project "$root" demo_flags
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_flags", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = {
    presets = { "warnings", "concurrency" },
    cxx_flags = { "-DE2E_RAW_FLAG=1" },
    link_libraries = { "m" }
  }
}
LUA

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with presets"
    return
  fi
  pass "builds with presets"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'CXX_COMPILER_ID:GNU,Clang,AppleClang>:-Wall' "warnings preset emitted"
  assert_contains "$cmake" 'find_package(Threads REQUIRED)' "concurrency preset requests Threads"
  assert_contains "$cmake" 'add_compile_options("-DE2E_RAW_FLAG=1")' "raw flag emitted"
  assert_contains "$cmake" 'link_libraries(m)' "extra library linked"
}
