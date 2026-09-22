# pkg-config channel: modules resolve through pkg_check_modules and their
# imported targets reach the compile and link lines.
scenario_65_pkgconfig() {
  local root="$WORK/65-pkgconfig"
  local fake="$root/fake"

  # A fake installed module: a header plus a .pc file.
  mkdir -p "$fake/include" "$fake/lib/pkgconfig" "$root/src"
  printf '#pragma once\ninline int fakewidget_value() { return 7; }\n' >"$fake/include/fakewidget.h"
  cat >"$fake/lib/pkgconfig/fakewidget.pc" <<PC
prefix=$fake
Name: fakewidget
Description: stub module for the test suite
Version: 1.0.0
Cflags: -I\${prefix}/include
Libs:
PC

  make_plain_project "$root" demo_pkgconfig
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_pkgconfig", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {}, pkgconfig = { "fakewidget" } },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <fakewidget.h>\n#include <cstdio>\nint main() { std::printf("widget=%%d\\n", fakewidget_value()); return 0; }\n' \
    >"$root/src/main.cpp"

  if ! command -v pkg-config >/dev/null 2>&1; then
    skip "pkg-config is not installed"
    return
  fi

  local old_pkg_config_path="${PKG_CONFIG_PATH:-}"
  export PKG_CONFIG_PATH="$fake/lib/pkgconfig${old_pkg_config_path:+:$old_pkg_config_path}"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with a pkg-config dependency"
    export PKG_CONFIG_PATH="$old_pkg_config_path"
    return
  fi
  pass "builds with a pkg-config dependency"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "find_package(PkgConfig REQUIRED)" "PkgConfig is found"
  assert_contains "$cmake" "pkg_check_modules(FAKEWIDGET REQUIRED IMPORTED_TARGET fakewidget)" \
    "the module is checked"
  assert_contains "$cmake" "PkgConfig::FAKEWIDGET" "the imported target is linked"
  assert_runs "$root/build/demo_pkgconfig" "widget=7" \
    "the module's include path reached the compiler"

  # A module that does not exist fails the configure with pkg-config's message.
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_pkgconfig", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {}, pkgconfig = { "no-such-module-xyz" } },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  assert_exit 1 "a missing module fails the build" forge_in "$root" build

  export PKG_CONFIG_PATH="$old_pkg_config_path"
}
