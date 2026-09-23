# The Lua build-script mechanism: several scripts, their merge order, value
# shapes, escaping, ${PROJECT_NAME} substitution and config.set persistence.
scenario_42_lua_scripts() {
  local root="$WORK/42-lua-scripts"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_lua2
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_lua2", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  custom = { SHARED_FLAG = "from-custom" }
}
LUA

  # a_first: variables (including values that need escaping) + a config write.
  cat >"$root/.config/forge/build/a_first.lua" <<'LUA'
-- Persists to forge.lua; the next build picks it up.
forge.config.set("PERSISTED_FLAG", "from-script")
return {
  cmakeOptions = {
    variables = {
      SHARED_FLAG = "from-script",
      SPACED = "two words",
      QUOTED = 'say "hi"',
      PAREN = "x)y",
      BACKSLASH = "a\\b",
      EMPTY = ""
    }
  }
}
LUA

  # b_second: a pre-phase snippet using ${PROJECT_NAME}, plus a definition.
  cat >"$root/.config/forge/build/b_second.lua" <<'LUA'
forge.add_cmake('set(NAME_MARKER "${PROJECT_NAME}")', "pre")
return { cmakeOptions = { definitions = { "SECOND=1" } } }
LUA

  # c_scalar: a single string where a list is expected.
  cat >"$root/.config/forge/build/c_scalar.lua" <<'LUA'
return { cmakeOptions = { definitions = "SINGLE_STRING=1" } }
LUA

  # d_notable / e_nothing: scripts that do not return a table at all.
  printf 'return 42\n' >"$root/.config/forge/build/d_notable.lua"
  printf 'local x = 1\n' >"$root/.config/forge/build/e_nothing.lua"

  local out flat
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds with several Lua scripts"
    tail -5 <<<"$out"
    return
  fi
  pass "builds with several Lua scripts"
  flat="$(flatten <<<"$out")"

  if grep -qF "Config set: PERSISTED_FLAG = from-script" <<<"$flat"; then
    pass "config.set reports the write"
  else
    fail "config.set reports the write"
  fi

  local cmake="$root/.config/cmake/CMakeLists.txt"

  # A script's variable wins over the declarative `custom` value, which is
  # emitted first.
  local custom_line script_line
  custom_line="$(grep -n 'set(SHARED_FLAG "from-custom")' "$cmake" | cut -d: -f1 || true)"
  script_line="$(grep -n 'set(SHARED_FLAG "from-script")' "$cmake" | cut -d: -f1 || true)"
  if [[ -n "$custom_line" && -n "$script_line" && "$custom_line" -lt "$script_line" ]]; then
    pass "script variable overrides the custom value (emitted after it)"
  else
    fail "script variable overrides the custom value (custom=$custom_line script=$script_line)"
  fi

  assert_contains "$cmake" 'set(SPACED "two words")' "value with spaces"
  assert_contains "$cmake" 'set(QUOTED "say \"hi\"")' "quotes are escaped"
  assert_contains "$cmake" 'set(PAREN "x)y")' "parenthesis in a value"
  assert_contains "$cmake" 'set(BACKSLASH "a\\b")' "backslash is escaped"
  assert_contains "$cmake" 'set(EMPTY "")' "empty value"
  assert_contains "$cmake" 'set(NAME_MARKER "demo_lua2")' "\${PROJECT_NAME} substituted"
  assert_contains "$cmake" "add_compile_definitions(SECOND=1)" "definitions from a second script"
  assert_contains "$cmake" "add_compile_definitions(SINGLE_STRING=1)" \
    "a single string works where a list is expected"

  # config.set persists to forge.lua; the next build emits it as a variable.
  assert_contains "$root/forge.lua" "PERSISTED_FLAG" "config.set persisted to forge.lua"
  if forge_in "$root" build >/dev/null 2>&1 &&
    grep -qF 'set(PERSISTED_FLAG "from-script")' "$cmake"; then
    pass "the persisted value is used by the next build"
  else
    fail "the persisted value is used by the next build"
  fi
}
