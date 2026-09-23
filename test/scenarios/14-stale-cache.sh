# A build/ configured for another source tree is regenerated, keeping _deps.
scenario_14_stale_cache() {
  local root="$WORK/14-stale-cache"
  make_dep_project "$root"
  forge_in "$root/app" build >/dev/null 2>&1 || true

  sed_in_place 's|^CMAKE_HOME_DIRECTORY:INTERNAL=.*|CMAKE_HOME_DIRECTORY:INTERNAL=/nonexistent/old-checkout|' \
    "$root/app/build/CMakeCache.txt"
  if grep -qF "CMAKE_HOME_DIRECTORY:INTERNAL=/nonexistent/old-checkout" \
    "$root/app/build/CMakeCache.txt"; then
    pass "stale cache planted"
  else
    fail "stale cache planted (sed did not apply)"
    return
  fi

  local out flat
  out="$(forge_in "$root/app" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "regenerating the cache" <<<"$flat"; then
    pass "detects the mismatch"
  else
    fail "detects the mismatch"
  fi
  if grep -qF "CMAKE_HOME_DIRECTORY:INTERNAL=$root/app" "$root/app/build/CMakeCache.txt"; then
    pass "reconfigures for the current source tree"
  else
    fail "reconfigures for the current source tree"
  fi
  assert_runs "$root/app/build/demo_app" "demo_value=42" "still runs afterwards"
}
