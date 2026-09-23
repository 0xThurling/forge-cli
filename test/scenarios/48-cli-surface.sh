# The CLI surface itself: version, help, no arguments, unknown commands and
# parent commands without a subcommand.
scenario_48_cli_surface() {
  local out flat

  out="$(forge --version 2>&1)"
  if [[ "$out" =~ ^[0-9]+\.[0-9]+ ]]; then
    pass "--version prints a version"
  else
    fail "--version prints a version (got '$(printf '%.60s' "$out")')"
  fi

  out="$(forge --help 2>&1)"
  flat="$(flatten <<<"$out")"
  for cmd in build clean create doctor project run test; do
    if grep -qF "$cmd" <<<"$flat"; then
      pass "--help lists '$cmd'"
    else
      fail "--help lists '$cmd'"
    fi
  done

  # No arguments: the usage text, and success.
  assert_exit 0 "no arguments succeeds" forge
  out="$(forge 2>&1)"
  flat="$(flatten <<<"$out")"
  if grep -qE "project manager|Usage|forge \[command\]" <<<"$flat"; then
    pass "no arguments prints usage"
  else
    fail "no arguments prints usage"
  fi

  # An unknown command is an error.
  assert_exit 1 "unknown command fails" forge frobnicate
  out="$(forge frobnicate 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Unrecognized command or argument 'frobnicate'" <<<"$flat"; then
    pass "the unknown command is named"
  else
    fail "the unknown command is named"
  fi

  # Parent commands without a subcommand print their help, without failing.
  assert_exit 0 "'new' without a subcommand succeeds" forge new
  assert_exit 0 "'project' without a subcommand succeeds" forge project
}
