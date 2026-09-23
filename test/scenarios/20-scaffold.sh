# `forge create` scaffolds a buildable project; `forge new *` adds files.
scenario_20_scaffold() {
  local root="$WORK/20-scaffold"
  mkdir -p "$root"
  local proj="$root/demo"

  if ! forge_in "$root" create demo >/dev/null 2>&1; then
    fail "forge create"
    return
  fi
  pass "forge create"

  for path in forge.lua src/main.cpp assets external \
    .config/forge/build .config/forge/definitions .gitignore; do
    assert_exists "$proj/$path" "scaffolds $path"
  done

  if ! forge_in "$proj" build >/dev/null 2>&1; then
    fail "the scaffolded project builds"
    return
  fi
  pass "the scaffolded project builds"

  forge_in "$proj" new class Widget >/dev/null 2>&1 || true
  assert_exists "$proj/src/Widget.h" "new class header"
  assert_exists "$proj/src/Widget.cpp" "new class source"
  forge_in "$proj" new header Only >/dev/null 2>&1 || true
  assert_exists "$proj/src/Only.h" "new header"
  forge_in "$proj" new source helper >/dev/null 2>&1 || true
  assert_exists "$proj/src/helper.cpp" "new source"
  forge_in "$proj" new struct Point >/dev/null 2>&1 || true
  assert_exists "$proj/src/Point.h" "new struct"

  # The added sources must not break the build (they are picked up by the glob).
  if forge_in "$proj" build >/dev/null 2>&1; then
    pass "still builds after adding sources"
  else
    fail "still builds after adding sources"
  fi

  # A library scaffold sets the library type and installs headers.
  local lib="$root/libdemo"
  if forge_in "$root" create libdemo --type library >/dev/null 2>&1; then
    pass "create --type library"
  else
    fail "create --type library"
  fi
  assert_contains "$lib/forge.lua" 'type = "library"' "library type recorded"
  assert_contains "$lib/forge.lua" "install_headers = true" "library installs headers"
  if forge_in "$lib" build >/dev/null 2>&1; then
    pass "scaffolded library builds"
  else
    fail "scaffolded library builds"
  fi

  # A scaffolded project ignores what Forge and the build generate, so it does
  # not start out dirty.
  assert_contains "$root/demo/.gitignore" "CMakePresets.json" \
    "the scaffold ignores CMakePresets.json"
  assert_contains "$root/demo/.gitignore" "compile_commands.json" \
    "the scaffold ignores the compile database"
}
