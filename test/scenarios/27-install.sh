# `forge install` is a no-op when nothing needs Conan (conan may not even be
# installed) — it must not fail the build.
scenario_27_install() {
  local root="$WORK/27-install"
  make_plain_project "$root" demo_install

  assert_exit 0 "forge install with no Conan packages" forge_in "$root" install

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds without Conan installed"
    return
  fi
  pass "builds without Conan installed"
}
