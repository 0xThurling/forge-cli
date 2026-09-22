# `forge.lua` itself: syntax errors, missing names, non-table returns and
# unknown top-level keys.
scenario_43_forge_lua_errors() {
  local base="$WORK/43-forge-lua-errors"

  # Syntax error in forge.lua.
  local syntax="$base/syntax"
  mkdir -p "$syntax/src"
  printf 'return {\n  project = { name = "broken"\n' >"$syntax/forge.lua"
  printf 'int main() { return 0; }\n' >"$syntax/src/main.cpp"
  local out flat
  out="$(forge_in "$syntax" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  assert_exit 1 "a forge.lua syntax error fails" forge_in "$syntax" build
  if grep -qiE "error loading forge.lua" <<<"$flat"; then
    pass "the syntax error is reported"
  else
    fail "the syntax error is reported (got '$(printf '%.90s' "$flat")')"
  fi

  # No project name.
  local noname="$base/no-name"
  mkdir -p "$noname/src"
  printf 'return { project = { type = "executable" } }\n' >"$noname/forge.lua"
  printf 'int main() { return 0; }\n' >"$noname/src/main.cpp"
  assert_exit 1 "a missing project name fails" forge_in "$noname" build
  out="$(forge_in "$noname" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Not a forge project" <<<"$flat"; then
    pass "the missing name is reported"
  else
    fail "the missing name is reported"
  fi

  # A non-table return.
  local scalar="$base/scalar-return"
  mkdir -p "$scalar/src"
  printf 'return 5\n' >"$scalar/forge.lua"
  printf 'int main() { return 0; }\n' >"$scalar/src/main.cpp"
  assert_exit 1 "a non-table forge.lua fails" forge_in "$scalar" build

  # Unknown top-level keys land in `custom` and are readable from Lua.
  local unknown="$base/unknown-keys"
  mkdir -p "$unknown/src" "$unknown/.config/forge/build"
  make_plain_project "$unknown" demo_unknown
  cat >"$unknown/forge.lua" <<'LUA'
return {
  project = { name = "demo_unknown", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  my_top_level_key = "kept"
}
LUA
  cat >"$unknown/.config/forge/build/probe.lua" <<'LUA'
forge.log.info("top_level = " .. tostring(forge.config.get("my_top_level_key")))
return {}
LUA
  out="$(forge_in "$unknown" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "top_level = kept" <<<"$flat"; then
    pass "unknown top-level keys are readable"
  else
    fail "unknown top-level keys are readable"
  fi
  # They are also emitted as CMake variables when they are valid identifiers.
  assert_contains "$unknown/.config/cmake/CMakeLists.txt" 'set(my_top_level_key "kept")' \
    "unknown top-level key emitted as a CMake variable"
}
