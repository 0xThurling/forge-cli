# `forge doctor` reports a healthy layout and a broken one, and is repeatable.
scenario_40_doctor_layout() {
  local healthy="$WORK/40-doctor-layout/healthy"
  mkdir -p "$healthy/src" "$healthy/.config/forge/build" "$healthy/assets" "$healthy/external"
  make_plain_project "$healthy" demo_healthy
  printf 'return {}\n' >"$healthy/.config/forge/build/noop.lua"

  local out flat
  out="$(forge_in "$healthy" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Project looks healthy" <<<"$flat"; then
    pass "healthy project reported"
  else
    fail "healthy project reported"
  fi
  if grep -qF "Build scripts found: 1" <<<"$flat"; then
    pass "build scripts counted"
  else
    fail "build scripts counted"
  fi

  # A bare project (no .config) is reported as missing required directories.
  local bare="$WORK/40-doctor-layout/bare"
  mkdir -p "$bare/src"
  make_plain_project "$bare" demo_bare
  out="$(forge_in "$bare" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF ".config/forge/ - Forge configuration (missing)" <<<"$flat"; then
    pass "missing required directory reported"
  else
    fail "missing required directory reported"
  fi
  if grep -qF "assets/ - Resource files (missing (optional))" <<<"$flat"; then
    pass "optional directory reported as optional"
  else
    fail "optional directory reported as optional"
  fi
}
