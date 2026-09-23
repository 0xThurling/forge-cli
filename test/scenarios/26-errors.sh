# Error paths are non-zero and say what is wrong, in terms of the current
# configuration file.
scenario_26_errors() {
  local empty="$WORK/26-errors/empty"
  mkdir -p "$empty"

  assert_exit 1 "build outside a project fails" forge_in "$empty" build

  local out flat
  out="$(forge_in "$empty" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Not a forge project" <<<"$flat"; then
    pass "build outside a project reports it"
  else
    fail "build outside a project reports it"
  fi
  if grep -qF "forge.lua" <<<"$flat"; then
    pass "the message names forge.lua"
  else
    fail "the message names forge.lua (got '$(printf '%.80s' "$flat")')"
  fi

  # An unknown preset warns but still builds.
  local root="$WORK/26-errors/unknown-preset"
  make_plain_project "$root" demo_preset
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_preset", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { presets = { "definitely-not-a-preset" } }
}
LUA
  if out="$(forge_in "$root" build 2>&1)"; then
    pass "unknown preset does not fail the build"
  else
    fail "unknown preset does not fail the build"
  fi
  flat="$(flatten <<<"$out")"
  if grep -qF "Unknown build preset 'definitely-not-a-preset'" <<<"$flat"; then
    pass "unknown preset is named in a warning"
  else
    fail "unknown preset is named in a warning"
  fi
}
