# `forge config migrate` turns a legacy package.toml into forge.lua.
scenario_37_config_migrate() {
  local root="$WORK/37-config-migrate"
  mkdir -p "$root"
  cat >"$root/package.toml" <<'TOML'
[project]
name = "legacy_app"
type = "executable"
standard = "17"

[conan-dependencies]
fmt = "10.2.1"
TOML

  assert_exit 0 "config migrate" forge_in "$root" config migrate
  assert_exists "$root/forge.lua" "forge.lua created"

  local out
  out="$(cat "$root/forge.lua")"
  if [[ "$out" == *legacy_app* ]]; then
    pass "project name migrated"
  else
    fail "project name migrated (got '$(printf '%.120s' "$out")')"
  fi
  if [[ "$out" == *17* ]]; then
    pass "standard migrated"
  else
    fail "standard migrated"
  fi
  if [[ "$out" == *fmt* ]]; then
    pass "conan package migrated"
  else
    fail "conan package migrated"
  fi

  # Migrating again is reported, not crashed on.
  assert_exit 0 "migrate is repeatable" forge_in "$root" config migrate

  # Without a package.toml it reports and fails. (The directory must not sit
  # under the migrated project: Forge searches upwards for a project root.)
  local empty="$WORK/37-migrate-empty"
  mkdir -p "$empty"
  assert_exit 1 "migrate without package.toml fails" forge_in "$empty" config migrate
}
