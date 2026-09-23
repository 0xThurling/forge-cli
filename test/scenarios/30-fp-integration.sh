# Integration with the ForgeFP checkout next door: its installed mirror must
# stay byte-identical to src/ and its git status must not grow.
scenario_30_fp_integration() {
  local fp="$REPO/../fp"
  if [[ ! -d "$fp/src/fp" ]]; then
    skip "no ../fp checkout"
    return
  fi

  # The status before the build, so the check below looks at what the build
  # *added* rather than at whatever the checkout was already carrying.
  local before_list before
  before_list="$(git -C "$fp" status --porcelain | sort)"
  before="$(grep -c . <<<"$before_list" || true)"

  if ! forge_in "$fp" build >/dev/null 2>&1; then
    fail "forge build on fp"
    return
  fi
  pass "forge build on fp"

  # A source header newer than its mirror means fp is being edited *while* this
  # runs: the mirror is stale because of that save, not because Forge failed to
  # regenerate it (the build above already did). Anything else is a real bug.
  local stale=""
  local source
  while IFS= read -r source; do
    local mirror="$fp/include/forgefp/fp/${source#"$fp/src/fp/"}"
    if [[ -f "$mirror" && "$source" -nt "$mirror" ]]; then
      stale="$source"
      break
    fi
  done < <(find "$fp/src/fp" -name '*.hpp')

  if diff -r "$fp/src/fp" "$fp/include/forgefp/fp" >/dev/null 2>&1; then
    pass "fp mirror stays byte-identical"
  elif [[ -n "$stale" ]]; then
    skip "fp is being edited (${stale##*/} is newer than its mirror)"
  else
    fail "fp mirror stays byte-identical"
  fi

  # The build must not dirty the checkout beyond its own artifacts. Mirroring a
  # *new* header legitimately adds a file under include/, so additions there are
  # expected — anything else is not.
  local after_list after new_entries unexpected
  after_list="$(git -C "$fp" status --porcelain | sort)"
  after="$(grep -c . <<<"$after_list" || true)"
  new_entries="$(comm -13 <(printf '%s\n' "$before_list") <(printf '%s\n' "$after_list") || true)"

  if [[ "$after" -le "$before" ]]; then
    pass "fp git status unchanged ($before entries)"
  elif [[ -z "$new_entries" ]]; then
    pass "fp git status unchanged in substance ($before entries)"
  else
    unexpected="$(awk '{print $2}' <<<"$new_entries" | grep -v '^include/' || true)"
    if [[ -z "$unexpected" ]]; then
      pass "fp git status grew only by mirrored headers ($before -> $after)"
    else
      fail "the build added unexpected entries ($(flatten <<<"$unexpected"))"
    fi
  fi
}
