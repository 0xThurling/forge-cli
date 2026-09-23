# Integration with the ForgeFP checkout next door: its installed mirror must
# stay byte-identical to src/ and its git status must not grow.
scenario_30_fp_integration() {
  local fp="$REPO/../fp"
  if [[ ! -d "$fp/src/fp" ]]; then
    skip "no ../fp checkout"
    return
  fi

  local before after
  before="$(git -C "$fp" status --porcelain | wc -l)"

  if ! forge_in "$fp" build >/dev/null 2>&1; then
    fail "forge build on fp"
    return
  fi
  pass "forge build on fp"

  if diff -r "$fp/src/fp" "$fp/include/forgefp/fp" >/dev/null 2>&1; then
    pass "fp mirror stays byte-identical"
  else
    fail "fp mirror stays byte-identical"
  fi

  after="$(git -C "$fp" status --porcelain | wc -l)"
  if [[ "$after" -le "$before" ]]; then
    pass "fp git status unchanged ($before entries)"
  else
    fail "fp git status grew ($before -> $after)"
  fi
}
