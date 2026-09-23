# The CLI's own C# stays formatted: `dotnet format` reports nothing to change.
# The style itself is advisory (see .editorconfig); this catches drift.
scenario_92_code_format() {
  if ! command -v dotnet >/dev/null 2>&1; then
    skip "dotnet is not installed"
    return
  fi

  local out
  if out="$(cd "$REPO" && timeout 600 dotnet format --verify-no-changes 2>&1)"; then
    pass "the C# sources are formatted"
  else
    fail "the C# sources are formatted: $(flatten <<<"$out" | tail -c 200)"
  fi
}
