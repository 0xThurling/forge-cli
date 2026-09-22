# A `path` that does not exist is reported with its resolved location.
scenario_12_missing_path() {
  local root="$WORK/12-missing-path"
  make_dep_project "$root"
  sed_in_place 's|path = "../lib"|path = "../nope"|' "$root/app/forge.lua"

  local out flat
  out="$(forge_in "$root/app" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"

  if grep -qF "points at '../nope', which does not exist" <<<"$flat"; then
    pass "warns with the offending path"
  else
    fail "warns with the offending path"
  fi
  if grep -qF "$root/nope" <<<"$flat"; then
    pass "prints the resolved location"
  else
    fail "prints the resolved location"
  fi
}
