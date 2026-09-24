# `forge watch`: build once, then rebuild when a source file changes, stopping
# after the requested number of rebuilds.
scenario_84_watch() {
  local root="$WORK/84-watch"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_watch

  # One pass: build and exit.
  local out flat
  if out="$(forge_in_timeout 120 "$root" watch --iterations 1 --interval 0.2 2>&1)"; then
    pass "watch builds and exits after one pass"
  else
    fail "watch builds and exits after one pass"
  fi
  flat="$(flatten <<<"$out")"
  if grep -qF "Build finished successfully" <<<"$flat"; then
    pass "the first pass built the project"
  else
    fail "the first pass built the project"
  fi
  if grep -qF "Stopped after 0 rebuild" <<<"$flat"; then
    pass "no change means no rebuild"
  else
    fail "no change means no rebuild (got '${flat:0:200}')"
  fi

  # A change while watching triggers exactly one more build. The touch lands
  # three seconds in, with the loop scanning every 0.2s.
  (sleep 3; touch "$root/src/main.cpp") &
  local toucher=$!
  if out="$(forge_in_timeout 120 "$root" watch --iterations 2 --interval 0.2 2>&1)"; then
    pass "watch stops after the requested run"
  else
    fail "watch stops after the requested rebuild"
  fi
  wait "$toucher" 2>/dev/null || true
  flat="$(flatten <<<"$out")"
  if grep -qF "change 1" <<<"$flat" && grep -qF "src/main.cpp" <<<"$flat"; then
    pass "the changed file is reported"
  else
    fail "the changed file is reported (got '${flat:0:200}')"
  fi
  if grep -qF "Stopped after 1 rebuild" <<<"$flat"; then
    pass "one rebuild happened"
  else
    fail "one rebuild happened (got '${flat:0:200}')"
  fi
  if grep -qF "configure 0.0s" <<<"$flat"; then
    pass "a rebuild does not reconfigure CMake"
  else
    fail "a rebuild does not reconfigure CMake"
  fi
  if grep -qF "rebuild finished in" <<<"$flat"; then
    pass "the rebuild time is reported"
  else
    fail "the rebuild time is reported"
  fi

  # --command test runs the suite instead of a plain build.
  out="$(forge_in_timeout 120 "$root" watch --command test --iterations 1 --interval 0.2 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Watching src" <<<"$flat"; then
    pass "the watched directories are reported"
  else
    fail "the watched directories are reported"
  fi

  # Unknown commands and an empty watch list are rejected.
  assert_exit 1 "an unknown command is rejected" forge_in "$root" watch --command nope
  local bare="$WORK/84-watch/bare"
  mkdir -p "$bare"
  make_plain_project "$bare" demo_watch_bare
  rm -rf "$bare/src"
  assert_exit 1 "an empty watch list is rejected" forge_in "$bare" watch --iterations 1
}
