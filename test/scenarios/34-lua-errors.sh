# A broken build script fails the build cleanly: a named message, no stack
# trace, non-zero exit — and the next build still works once it is fixed.
scenario_34_lua_errors() {
  local base="$WORK/34-lua-errors"

  # Runtime error: indexing nil.
  local runtime="$base/runtime"
  mkdir -p "$runtime/src" "$runtime/.config/forge/build"
  make_plain_project "$runtime" demo_runtime
  cat >"$runtime/.config/forge/build/broken.lua" <<'LUA'
local t = nil
t.field = 1
return {}
LUA
  local out flat
  out="$(forge_in "$runtime" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "build script 'broken.lua' failed" <<<"$flat"; then
    pass "runtime error names the script"
  else
    fail "runtime error names the script (got '$(printf '%.100s' "$flat")')"
  fi
  if grep -qF "Unhandled exception" <<<"$flat"; then
    fail "runtime error does not crash the CLI"
  else
    pass "runtime error does not crash the CLI"
  fi
  assert_exit 1 "runtime error fails the build" forge_in "$runtime" build

  # Syntax error.
  local syntax="$base/syntax"
  mkdir -p "$syntax/src" "$syntax/.config/forge/build"
  make_plain_project "$syntax" demo_syntax
  printf 'return {\n' >"$syntax/.config/forge/build/syntax.lua"
  out="$(forge_in "$syntax" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "build script 'syntax.lua' failed" <<<"$flat"; then
    pass "syntax error names the script"
  else
    fail "syntax error names the script"
  fi
  assert_exit 1 "syntax error fails the build" forge_in "$syntax" build

  # Fixing the script lets the build through again.
  printf 'return { cmakeOptions = { definitions = { "FIXED=1" } } }\n' \
    >"$syntax/.config/forge/build/syntax.lua"
  assert_exit 0 "a fixed script builds" forge_in "$syntax" build
}
