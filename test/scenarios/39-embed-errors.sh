# `forge embed` failure paths, and the resource list in forge.lua.
scenario_39_embed_errors() {
  local root="$WORK/39-embed-errors"
  make_plain_project "$root" demo_embed

  assert_exit 1 "missing file fails" forge_in "$root" embed nope.bin
  local out flat
  out="$(forge_in "$root" embed nope.bin 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "File not found" <<<"$flat"; then
    pass "missing file is reported"
  else
    fail "missing file is reported"
  fi

  # Outside a project the configuration is missing. (The directory must not sit
  # under the project above: Forge searches upwards for a project root.)
  local empty="$WORK/39-embed-empty"
  mkdir -p "$empty"
  printf 'x\n' >"$empty/asset.bin"
  assert_exit 1 "embed outside a project fails" forge_in "$empty" embed asset.bin

  # A directory is not a resource file.
  mkdir -p "$root/assets/dir"
  assert_exit 1 "embedding a directory fails" forge_in "$root" embed assets/dir

  # A path outside the project would make the build depend on a sibling file.
  printf 'outside\n' >"$WORK/39-outside.txt"
  assert_exit 1 "embedding outside the project fails" \
    forge_in "$root" embed "$WORK/39-outside.txt"
  out="$(forge_in "$root" embed "$WORK/39-outside.txt" 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "is outside the project" <<<"$flat"; then
    pass "the outside path is explained"
  else
    fail "the outside path is explained"
  fi
}
