# `forge run`: no argument builds and runs the executable; a library is
# reported; an unknown script fails.
scenario_44_run_command() {
  local exe="$WORK/44-run-command/exe"
  make_plain_project "$exe" demo_run

  local out flat
  if ! out="$(forge_in "$exe" run 2>&1)"; then
    fail "forge run builds and runs the executable"
    tail -5 <<<"$out"
    return
  fi
  pass "forge run builds and runs the executable"
  flat="$(flatten <<<"$out")"
  if grep -qF "hello from demo_run" <<<"$flat"; then
    pass "the program's output is shown"
  else
    fail "the program's output is shown"
  fi
  if grep -qF "Running:" <<<"$flat"; then
    pass "the executable path is reported"
  else
    fail "the executable path is reported"
  fi

  # A library cannot be executed; that is reported, not an error.
  local lib="$WORK/44-run-command/lib"
  mkdir -p "$lib/src"
  cat >"$lib/forge.lua" <<'LUA'
return {
  project = { name = "demo_runlib", type = "library", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int f() { return 1; }\n' >"$lib/src/lib.cpp"
  out="$(forge_in "$lib" run 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Libraries cannot be executed directly" <<<"$flat"; then
    pass "running a library is reported"
  else
    fail "running a library is reported"
  fi

  # An unknown script name fails with the name in the message.
  assert_exit 1 "an unknown script fails" forge_in "$exe" run nope
  out="$(forge_in "$exe" run nope 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Script 'nope' not found" <<<"$flat"; then
    pass "the unknown script is named"
  else
    fail "the unknown script is named"
  fi
}
