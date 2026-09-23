# `forge clean` removes the build directory (and is safe to repeat).
scenario_25_clean() {
  local root="$WORK/25-clean"
  make_plain_project "$root" demo_clean
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_exists "$root/build" "build directory exists after building"

  if forge_in "$root" clean >/dev/null 2>&1; then
    pass "forge clean"
  else
    fail "forge clean"
  fi
  assert_missing "$root/build" "build directory removed"

  # The compile-database symlink points into build/, so it goes too.
  if [[ -L "$root/compile_commands.json" ]]; then
    fail "the compile database symlink is removed"
  else
    pass "the compile database symlink is removed"
  fi

  assert_exit 0 "clean is idempotent" forge_in "$root" clean
}
