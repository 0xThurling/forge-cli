# Build toggles: unity builds, precompiled headers, and module scanning.
scenario_70_build_toggles() {
  local base="$WORK/70-build-toggles"

  # --- unity build ----------------------------------------------------------
  # Two independent translation units: without unity they compile to two
  # objects, with unity they are merged into one.
  local unity="$base/unity"
  mkdir -p "$unity/src"
  make_plain_project "$unity" demo_unity
  printf 'static int helper_a() { return 1; }\nint a_value() { return helper_a(); }\n' \
    >"$unity/src/a.cpp"
  printf 'static int helper_b() { return 2; }\nint b_value() { return helper_b(); }\n' \
    >"$unity/src/b.cpp"
  printf '#include <cstdio>\nint a_value();\nint b_value();\nint main() { std::printf("%%d\\n", a_value() + b_value()); return 0; }\n' \
    >"$unity/src/main.cpp"

  local objects="$unity/build/CMakeFiles/demo_unity.dir"
  forge_in "$unity" build >/dev/null 2>&1 || true
  if [[ -f "$objects/src/a.cpp.o" && -f "$objects/src/b.cpp.o" ]]; then
    pass "without unity each source is compiled separately"
  else
    fail "without unity each source is compiled separately"
  fi
  assert_runs "$unity/build/demo_unity" "3" "the non-unity binary runs"

  cat >"$unity/forge.lua" <<'LUA'
return {
  project = { name = "demo_unity", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { unity = true }
}
LUA

  # Start clean, so the object layout is unambiguous.
  rm -rf "$unity/build"
  if ! forge_in "$unity" build >/dev/null 2>&1; then
    fail "the unity build succeeds"
  else
    pass "the unity build succeeds"
  fi
  if [[ -f "$objects/Unity/unity_0_cxx.cxx.o" ]]; then
    pass "unity merges the sources into one translation unit"
  else
    fail "unity merges the sources into one translation unit"
  fi
  if [[ -f "$objects/src/a.cpp.o" || -f "$objects/src/b.cpp.o" ]]; then
    fail "the separate objects are gone"
  else
    pass "the separate objects are gone"
  fi
  assert_contains "$unity/.config/cmake/CMakeLists.txt" "set(CMAKE_UNITY_BUILD ON)" \
    "CMAKE_UNITY_BUILD is emitted"
  assert_runs "$unity/build/demo_unity" "3" "the unity binary runs"

  # --- precompiled header ---------------------------------------------------
  local pch="$base/pch"
  mkdir -p "$pch/src"
  make_plain_project "$pch" demo_pch
  cat >"$pch/src/pch.hpp" <<'HPP'
#pragma once
#define FROM_PCH 42
HPP
  cat >"$pch/forge.lua" <<'LUA'
return {
  project = { name = "demo_pch", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { pch = "src/pch.hpp" }
}
LUA
  # main.cpp never includes pch.hpp: the value only arrives via the PCH.
  printf '#include <cstdio>\nint main() { std::printf("%%d\\n", FROM_PCH); return 0; }\n' \
    >"$pch/src/main.cpp"

  if ! forge_in "$pch" build >/dev/null 2>&1; then
    fail "the precompiled header is applied"
    return
  fi
  pass "the precompiled header is applied"
  assert_contains "$pch/.config/cmake/CMakeLists.txt" \
    "target_precompile_headers(demo_pch PRIVATE \${PROJECT_SOURCE_DIR}/src/pch.hpp)" \
    "target_precompile_headers is emitted"
  assert_runs "$pch/build/demo_pch" "42" "the PCH macro reaches the binary"

  # --- module scanning ------------------------------------------------------
  local modules="$base/modules"
  mkdir -p "$modules/src"
  make_plain_project "$modules" demo_modules
  cat >"$modules/forge.lua" <<'LUA'
return {
  project = { name = "demo_modules", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { modules = true }
}
LUA
  if ! command -v ninja >/dev/null 2>&1; then
    skip "ninja is not installed (module scanning needs it)"
  elif ! forge_in "$modules" build >/dev/null 2>&1; then
    fail "module scanning is enabled without breaking the build"
  else
    pass "module scanning is enabled without breaking the build"
  fi
  assert_contains "$modules/.config/cmake/CMakeLists.txt" "set(CMAKE_CXX_SCAN_FOR_MODULES ON)" \
    "CMAKE_CXX_SCAN_FOR_MODULES is emitted"
  if command -v ninja >/dev/null 2>&1; then
    assert_contains "$modules/build/CMakeCache.txt" "CMAKE_GENERATOR:INTERNAL=Ninja" \
      "modules switch the generator to Ninja"
    assert_runs "$modules/build/demo_modules" "hello from demo_modules" \
      "the module-scanned binary runs"
  fi

  # An explicit generator that cannot scan is reported, not silently broken.
  cat >"$modules/forge.lua" <<'LUA'
return {
  project = { name = "demo_modules", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { modules = true, generator = "Unix Makefiles" }
}
LUA
  rm -rf "$modules/build"
  local out
  out="$(forge_in "$modules" build 2>&1 || true)"
  if grep -qF "cannot work with" <<<"$(flatten <<<"$out")"; then
    pass "an incompatible generator is reported"
  else
    fail "an incompatible generator is reported"
  fi

  # --- the toggles survive a config rewrite ---------------------------------
  mkdir -p "$base/sibling"
  make_plain_project "$base/sibling" sibling
  forge_in "$modules" add sibling --path ../sibling >/dev/null 2>&1 || true
  assert_contains "$modules/forge.lua" "modules = true" "modules survives a rewrite"
  assert_contains "$pch/forge.lua" 'pch = "src/pch.hpp"' "pch survives a rewrite"
  forge_in "$unity" add sibling --path ../sibling >/dev/null 2>&1 || true
  assert_contains "$unity/forge.lua" "unity = true" "unity survives a rewrite"
}
