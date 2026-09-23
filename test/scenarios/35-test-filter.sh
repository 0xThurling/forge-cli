# `forge test` with a filter runs a subset, and a failing test suite fails the
# command (needs the ForgeFP checkout's googletest).
scenario_35_test_filter() {
  local fp="$REPO/../fp"
  if [[ ! -d "$fp/build/_deps/googletest-src" ]]; then
    skip "no googletest checkout in ../fp/build"
    return
  fi

  local out flat
  if ! out="$(forge_in "$fp" test --filter 'Ops.*' 2>&1)"; then
    fail "forge test --filter"
    tail -6 <<<"$out"
    return
  fi
  pass "forge test --filter"
  flat="$(flatten <<<"$out")"
  if grep -qF "All tests passed" <<<"$flat"; then
    pass "filtered suite passes"
  else
    fail "filtered suite passes"
  fi

  # A filter that matches nothing must still be reported clearly.
  out="$(forge_in "$fp" test --filter 'NoSuchSuite.*' 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qE "No tests were found|All tests passed|Tests failed" <<<"$flat"; then
    pass "an empty filter is reported"
  else
    fail "an empty filter is reported"
  fi
}
