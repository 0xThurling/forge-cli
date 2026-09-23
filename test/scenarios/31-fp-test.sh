# `forge test` builds and runs a project's test suite (needs a googletest
# dependency; the ForgeFP checkout next door already has one).
scenario_31_fp_test() {
  local fp="$REPO/../fp"
  if [[ ! -d "$fp/src/fp" ]]; then
    skip "no ../fp checkout"
    return
  fi
  if [[ ! -d "$fp/build/_deps/googletest-src" ]]; then
    skip "no googletest checkout in ../fp/build (run forge build there first)"
    return
  fi

  local out flat
  if ! out="$(forge_in "$fp" test 2>&1)"; then
    fail "forge test on fp"
    tail -8 <<<"$out"
    return
  fi
  pass "forge test on fp"

  flat="$(flatten <<<"$out")"
  if grep -qF "All tests passed" <<<"$flat"; then
    pass "reports the suite green"
  else
    fail "reports the suite green (got '$(printf '%.120s' "$flat")')"
  fi
}
