# `forge doctor` reports the project layout without failing.
scenario_21_doctor() {
  local root="$WORK/21-doctor"
  make_plain_project "$root" demo_doctor
  mkdir -p "$root/.config/forge/build"
  printf 'return {}\n' >"$root/.config/forge/build/noop.lua"

  local out flat
  if ! out="$(forge_in "$root" doctor 2>&1)"; then
    fail "exits 0 on a healthy project"
    tail -5 <<<"$out"
    return
  fi
  pass "exits 0 on a healthy project"

  flat="$(flatten <<<"$out")"
  if grep -qF "Project looks healthy" <<<"$flat"; then
    pass "reports the project healthy"
  else
    fail "reports the project healthy"
  fi
  if grep -qF ".config/forge/build/ - Build scripts (exists)" <<<"$flat"; then
    pass "reports the build-script directory"
  else
    fail "reports the build-script directory"
  fi
}
