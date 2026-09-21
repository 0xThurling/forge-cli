# A Lua build script's cmakeOptions reach the generated CMake (pre/post phases).
scenario_16_lua_script() {
  local root="$WORK/16-lua-script"
  mkdir -p "$root/src" "$root/.config/forge/build"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_lua", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  custom = { DEMO_CUSTOM = "declared" }
}
LUA
  printf 'payload\n' >"$root/payload.txt"
  tar -cf "$root/payload.tar" -C "$root" payload.txt

  cat >"$root/.config/forge/build/setup.lua" <<'LUA'
-- Stands in for a WebGPU-style setup: prepare something, then declare it.
-- The 2-argument extract must default stripComponents rather than throw.
forge.extract("payload.tar", "unpacked")
forge.add_cmake('set(PRE_MARKER "pre")', "pre")
forge.add_cmake('set(POST_MARKER "post")')
return {
  cmakeOptions = {
    variables = { DEMO_SDK = "/opt/demo" },
    findPackages = { "Threads" },
    definitions = { "DEMO_FROM_SCRIPT=7" },
    compileOptions = { "-DDEMO_FLAG=1" },
    linkLibraries = { "m" }
  }
}
LUA
  cat >"$root/src/main.cpp" <<'CPP'
#include <cstdio>
int main() {
#ifndef DEMO_FROM_SCRIPT
#error "the script's definition did not reach the compiler"
#endif
  std::printf("script_def=%d\n", DEMO_FROM_SCRIPT);
  return 0;
}
CPP

  local out
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds with a lua build script"
    tail -5 <<<"$out"
    return
  fi
  pass "builds with a lua build script"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'set(DEMO_CUSTOM "declared")' "custom entries become CMake variables"
  assert_contains "$cmake" 'set(DEMO_SDK "/opt/demo")' "script variables emitted"
  assert_contains "$cmake" 'find_package(Threads REQUIRED)' "find_package emitted"
  assert_contains "$cmake" 'add_compile_definitions(DEMO_FROM_SCRIPT=7)' "definitions emitted"
  assert_contains "$cmake" 'add_compile_options("-DDEMO_FLAG=1")' "compile options emitted"
  assert_contains "$cmake" 'link_libraries(m)' "link libraries emitted"

  # A "pre" snippet must land before the target is created, the default after.
  local pre_line target_line post_line
  pre_line="$(grep -n 'PRE_MARKER' "$cmake" | cut -d: -f1 || true)"
  target_line="$(grep -n 'add_executable' "$cmake" | cut -d: -f1 || true)"
  post_line="$(grep -n 'POST_MARKER' "$cmake" | cut -d: -f1 || true)"
  if [[ -n "$pre_line" && -n "$target_line" && -n "$post_line" &&
    "$pre_line" -lt "$target_line" && "$target_line" -lt "$post_line" ]]; then
    pass "pre snippet before the target, post snippet after"
  else
    fail "pre snippet before the target, post snippet after (pre=$pre_line target=$target_line post=$post_line)"
  fi

  assert_runs "$root/build/demo_lua" "script_def=7" "the definition reached the compiler"
  assert_exists "$root/unpacked/payload.txt" "forge.extract(archive, dir) defaults stripComponents"
}
