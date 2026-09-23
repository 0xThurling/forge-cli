# Multi-target projects: extra executables and libraries beside the project's
# own target, with their own sources, link lines and install rules.
scenario_88_multi_target() {
  local root="$WORK/88-multi-target"
  mkdir -p "$root/src" "$root/tools" "$root/lib/shared" "$root/test"
  make_plain_project "$root" demo_multi

  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_multi", type = "executable", standard = "20" },
  targets = {
    { name = "tools", type = "executable", sources = { "tools" } },
    { name = "shared", type = "library", sources = { "lib/shared" } }
  },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = true
}
LUA
  printf '#pragma once\nint shared_value();\n' >"$root/lib/shared/shared.hpp"
  printf '#include "shared.hpp"\nint shared_value() { return 21; }\n' >"$root/lib/shared/shared.cpp"
  printf '#include <cstdio>\n#include "shared.hpp"\nint main() { std::printf("app=%%d\\n", shared_value() * 2); return 0; }\n' \
    >"$root/src/main.cpp"
  printf '#include <cstdio>\n#include "shared.hpp"\nint main() { std::printf("tools=%%d\\n", shared_value()); return 0; }\n' \
    >"$root/tools/main.cpp"
  printf '#include <gtest/gtest.h>\n#include "shared.hpp"\nTEST(Multi, Shared) { EXPECT_EQ(shared_value(), 21); }\n' \
    >"$root/test/multi_test.cpp"

  # --- all targets are built ------------------------------------------------
  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "a project with extra targets builds"
    return
  fi
  pass "a project with extra targets builds"
  assert_exists "$root/build/demo_multi" "the project's own target is built"
  assert_exists "$root/build/tools" "an extra executable is built"
  assert_exists "$root/build/libshared.a" "an extra library is built"

  # --- the generated CMake --------------------------------------------------
  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "add_executable(tools \${TOOLS_SOURCES})" "the extra executable is emitted"
  assert_contains "$cmake" "add_library(shared STATIC \${SHARED_SOURCES})" "the extra library is emitted"
  assert_contains "$cmake" "target_include_directories(shared PUBLIC \${PROJECT_SOURCE_DIR}/lib/shared)" \
    "the library's headers are visible to the project"
  assert_contains "$cmake" "target_link_libraries(demo_multi PRIVATE shared)" \
    "the project's own target links the extra library"
  assert_contains "$cmake" "target_link_libraries(tools PRIVATE shared)" \
    "the extra executable links the extra library"
  if grep -m1 'target_link_libraries(${PROJECT_NAME}_tests' "$cmake" | grep -qF "shared"; then
    pass "the test target links the extra library"
  else
    fail "the test target links the extra library"
  fi

  # --- running a chosen target ---------------------------------------------
  assert_runs "$root/build/demo_multi" "app=42" "the project's own target runs"
  local out
  out="$(forge_in "$root" run --no-build --bin tools 2>&1 || true)"
  if grep -qF "tools=21" <<<"$(flatten <<<"$out")"; then
    pass "forge run --bin runs an extra target"
  else
    fail "forge run --bin runs an extra target (got '$(flatten <<<"$out" | head -c 160)')"
  fi

  # A name that is not a target is reported with the available ones.
  out="$(forge_in "$root" run --no-build --bin nope 2>&1 || true)"
  if grep -qF "Targets:" <<<"$(flatten <<<"$out")"; then
    pass "an unknown target lists the known ones"
  else
    fail "an unknown target lists the known ones"
  fi

  # --- install rules: libraries ship, executables do not --------------------
  forge_in "$root" install --prefix out >/dev/null 2>&1 || true
  assert_exists "$root/out/lib/libshared.a" "the extra library is installed"
  assert_missing "$root/out/bin/demo_multi" "executables are not installed by default"

  # --- the configuration round-trips ---------------------------------------
  forge_in "$root" add other --path ../shared >/dev/null 2>&1 || true
  assert_contains "$root/forge.lua" "targets = {" "a config rewrite keeps the targets"
  assert_contains "$root/forge.lua" 'name = "tools"' "the target names survive"

  # --- info ----------------------------------------------------------------
  out="$(forge_in "$root" project info --json 2>&1 || true)"
  if [[ "$out" == *'"name":"tools"'* && "$out" == *'"name":"shared"'* ]]; then
    pass "project info --json lists the targets"
  else
    fail "project info --json lists the targets (got '${out:0:160}')"
  fi

  # --- validation ----------------------------------------------------------
  local bad="$WORK/88-multi-target/bad"
  mkdir -p "$bad/src"
  make_plain_project "$bad" demo_bad
  cat >"$bad/forge.lua" <<'LUA'
return {
  project = { name = "demo_bad", type = "executable", standard = "20" },
  targets = {
    { name = "bad name", sources = { "src" } },
    { name = "demo_bad", sources = { "src" } },
    { name = "missing", sources = { "nowhere" } }
  },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  out="$(forge_in "$bad" build 2>&1 || true)"
  local flat
  flat="$(flatten <<<"$out")"
  if grep -qF "unusable name" <<<"$flat"; then
    pass "a target with an unusable name is reported"
  else
    fail "a target with an unusable name is reported"
  fi
  if grep -qF "the name is already used" <<<"$flat"; then
    pass "a target clashing with the project is reported"
  else
    fail "a target clashing with the project is reported"
  fi
  if grep -qF "which does not exist" <<<"$flat"; then
    pass "a missing sources directory is reported"
  else
    fail "a missing sources directory is reported"
  fi
}
