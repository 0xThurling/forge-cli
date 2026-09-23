# CLI overrides: --standard, --release/--debug, --preset, --no-config-presets.
scenario_17_build_flags() {
  local root="$WORK/17-build-flags"
  make_plain_project "$root" demo_flags
  local cmake="$root/.config/cmake/CMakeLists.txt"

  # --standard overrides the configured standard for this invocation.
  forge_in "$root" build --standard 11 >/dev/null 2>&1 || true
  assert_contains "$cmake" "set(CMAKE_CXX_STANDARD 11)" "--standard applies"

  # Without the flag the configured standard wins.
  sed_in_place 's/standard = "20"/standard = "14"/' "$root/forge.lua"
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$cmake" "set(CMAKE_CXX_STANDARD 14)" "configured standard wins"

  # A config without `standard` still emits the 20 default.
  sed_in_place 's/, standard = "14"//' "$root/forge.lua"
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$cmake" "set(CMAKE_CXX_STANDARD 20)" "default standard emitted"

  # --release / --debug choose CMAKE_BUILD_TYPE.
  forge_in "$root" build --release >/dev/null 2>&1 || true
  assert_contains "$root/build/CMakeCache.txt" "CMAKE_BUILD_TYPE:STRING=Release" \
    "--release sets the build type"
  forge_in "$root" build --debug >/dev/null 2>&1 || true
  assert_contains "$root/build/CMakeCache.txt" "CMAKE_BUILD_TYPE:STRING=Debug" \
    "--debug sets the build type"

  # --preset adds a preset for one invocation; --no-config-presets drops the
  # configured ones.
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_flags", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { presets = { "warnings" } }
}
LUA
  forge_in "$root" build --preset simd >/dev/null 2>&1 || true
  assert_contains "$cmake" "march=native" "--preset adds a preset"
  assert_contains "$cmake" "CXX_COMPILER_ID:GNU,Clang,AppleClang>:-Wall" \
    "--preset keeps the configured presets"

  forge_in "$root" build --no-config-presets >/dev/null 2>&1 || true
  assert_lacks "$cmake" "march=native" "--no-config-presets drops the CLI preset"
  assert_lacks "$cmake" "CXX_COMPILER_ID:GNU,Clang,AppleClang>:-Wall" \
    "--no-config-presets drops the configured presets"
}
