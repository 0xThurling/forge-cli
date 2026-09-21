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
}
