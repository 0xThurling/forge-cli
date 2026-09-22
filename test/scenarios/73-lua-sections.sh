# Lua-registered CMake sections: anchors, ordering, replacement and validation.
scenario_73_lua_sections() {
  local root="$WORK/73-lua-sections"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_sections
  cat >"$root/.config/forge/build/sections.lua" <<'LUA'
forge.add_section("defines", "after:project_target", [[
target_compile_definitions(${PROJECT_NAME} PRIVATE FROM_LUA_SECTION=7)
]])

forge.add_section("early", "first", [[
set(LUA_EARLY_ON 1)
]])

forge.add_section("tail", [[
set(LUA_TAIL_ON 1)
]])
LUA
  cat >"$root/.config/forge/build/extra.lua" <<'LUA'
forge.add_section("extra", "before:project_target", "set(LUA_EXTRA_ON 1)")
LUA
  printf '#include <cstdio>\nint main() { std::printf("%%d\\n", FROM_LUA_SECTION); return 0; }\n' \
    >"$root/src/main.cpp"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "a project with Lua sections builds"
    return
  fi
  pass "a project with Lua sections builds"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  # ${PROJECT_NAME} is substituted when the section is emitted.
  assert_contains "$cmake" "target_compile_definitions(demo_sections PRIVATE FROM_LUA_SECTION=7)" \
    "the section content is emitted with the project name substituted"
  assert_contains "$cmake" "set(LUA_EXTRA_ON 1)" "a second build script contributes sections"
  assert_runs "$root/build/demo_sections" "7" "a Lua section can set target properties"

  # The anchors decide the order; unanchored sections go last.
  assert_order "$(flatten <"$cmake")" "the anchors place the sections" \
    "early (from Lua)" "set(LUA_EXTRA_ON 1)" "Project Target" "defines (from Lua)" "tail (from Lua)"

  # --- a repeated name replaces the earlier section -------------------------
  cat >"$root/.config/forge/build/sections.lua" <<'LUA'
forge.add_section("defines", "after:project_target", [[
target_compile_definitions(${PROJECT_NAME} PRIVATE FROM_LUA_SECTION=7)
]])

forge.add_section("early", "first", "set(LUA_EARLY_ON 2)")
LUA
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$cmake" "set(LUA_EARLY_ON 2)" "a repeated section name replaces the old one"
  assert_lacks "$cmake" "set(LUA_EARLY_ON 1)" "the replaced content is gone"

  # --- an unknown anchor is reported and the section still lands somewhere ---
  cat >"$root/.config/forge/build/extra.lua" <<'LUA'
forge.add_section("mystery", "after:nope", "set(LUA_MYSTERY_ON 1)")
LUA
  local out
  out="$(forge_in "$root" build 2>&1 || true)"
  local anchor_output
  anchor_output="$(flatten <<<"$out")"
  if grep -qF "anchors to unknown section" <<<"$anchor_output"; then
    pass "an unknown anchor is reported"
  else
    fail "an unknown anchor is reported (got '${anchor_output:0:200}')"
  fi
  if grep -qF "known: standard, features" <<<"$anchor_output"; then
    pass "the warning lists the known sections"
  else
    fail "the warning lists the known sections"
  fi
  assert_contains "$cmake" "set(LUA_MYSTERY_ON 1)" "the section is emitted anyway"

  # --- an unreadable position and an invalid name ---------------------------
  cat >"$root/.config/forge/build/extra.lua" <<'LUA'
forge.add_section("odd", "sideways", "set(LUA_ODD_ON 1)")
forge.add_section("bad name", "last", "set(LUA_BAD_ON 1)")
LUA
  out="$(forge_in "$root" build 2>&1 || true)"
  local flat
  flat="$(flatten <<<"$out")"
  if grep -qF "unreadable position" <<<"$flat"; then
    pass "an unreadable position is reported"
  else
    fail "an unreadable position is reported"
  fi
  if grep -qF "must start with a letter" <<<"$flat"; then
    pass "an invalid section name is rejected"
  else
    fail "an invalid section name is rejected"
  fi
  assert_contains "$cmake" "set(LUA_ODD_ON 1)" "a section with a bad position is still emitted"
  assert_lacks "$cmake" "set(LUA_BAD_ON 1)" "a section with an invalid name is dropped"
}
