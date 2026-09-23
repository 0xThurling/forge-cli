# `forge init`: adopt an existing directory, keeping what is already there.
scenario_85_init() {
  local root="$WORK/85-init/repo"
  mkdir -p "$root/src"

  # An existing checkout: sources the user already has.
  printf '#include <cstdio>\nint existing() { return 7; }\n' >"$root/src/existing.cpp"
  printf '# a project without a .gitignore\n' >"$root/README.md"

  if ! forge_in "$root" init --name mylib --type library --standard 17 --testing >/dev/null 2>&1; then
    fail "forge init"
    return
  fi
  pass "forge init"

  assert_exists "$root/forge.lua" "forge.lua is written"
  assert_contains "$root/forge.lua" 'name = "mylib"' "the name is used"
  assert_contains "$root/forge.lua" 'type = "library"' "the type is used"
  assert_contains "$root/forge.lua" 'standard = "17"' "the standard is used"
  assert_contains "$root/forge.lua" "testing = true" "the test target is enabled"
  assert_contains "$root/forge.lua" "install_headers = true" "a library installs its headers"

  # The layout, the ignore file, and the editor stubs.
  for path in src external assets .config/forge/build .config/forge/commands .config/forge/templates; do
    assert_exists "$root/$path" "init created $path"
  done
  assert_contains "$root/.gitignore" "CMakePresets.json" "the ignore file covers the generated files"
  assert_exists "$root/.config/forge/definitions/definitions.lua" "the Lua stubs are written"

  # Existing files are untouched, and no starter main.cpp is added to a library.
  assert_contains "$root/src/existing.cpp" "int existing()" "existing sources are kept"
  assert_contains "$root/README.md" "a project without" "other files are kept"
  assert_missing "$root/src/main.cpp" "no starter source is added to a library"

  # The initialised directory builds.
  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "the initialised project builds"
  else
    pass "the initialised project builds"
  fi

  # Running it twice is refused rather than overwriting.
  local out
  out="$(forge_in "$root" init 2>&1 || true)"
  if grep -qF "already has a \`forge.lua\`" <<<"$(flatten <<<"$out")"; then
    pass "an existing project is not re-initialised"
  else
    fail "an existing project is not re-initialised"
  fi

  # --- an executable in an empty directory ---------------------------------
  local empty="$WORK/85-init/fresh"
  mkdir -p "$empty"
  if ! forge_in "$empty" init --name app >/dev/null 2>&1; then
    fail "forge init in an empty directory"
  else
    pass "forge init in an empty directory"
  fi
  assert_exists "$empty/src/main.cpp" "an executable gets a starter source"
  assert_contains "$empty/forge.lua" "testing = false" "testing is off by default"

  # --- invalid input --------------------------------------------------------
  local bad="$WORK/85-init/bad"
  mkdir -p "$bad"
  assert_exit 1 "an unknown type is rejected" forge_in "$bad" init --type module
  assert_exit 1 "a name with spaces is rejected" forge_in "$bad" init --name "my lib"
}
