# `forge publish`: CPack packaging for libraries and executables, plus the
# failure paths (no version, no build, bad generator).
scenario_74_publish() {
  local base="$WORK/74-publish"

  # --- a versioned library --------------------------------------------------
  local lib="$base/lib"
  mkdir -p "$lib/src" "$lib/include"
  cat >"$lib/forge.lua" <<'LUA'
return {
  project = {
    name = "demo_pub", type = "library", standard = "20", version = "1.2.3",
    description = "A demo library", contact = "dev@example.com", install_headers = true
  },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#pragma once\nnamespace demo { int answer(); }\n' >"$lib/include/demo.hpp"
  printf 'namespace demo { int answer() { return 42; } }\n' >"$lib/src/lib.cpp"

  if ! forge_in "$lib" build >/dev/null 2>&1; then
    fail "a versioned library builds"
    return
  fi
  pass "a versioned library builds"

  local cmake="$lib/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "include(CPack)" "the packaging section is emitted"
  assert_contains "$cmake" 'set(CPACK_PACKAGE_VERSION "1.2.3")' "the version reaches CPack"
  assert_contains "$cmake" "CPACK_PACKAGE_DESCRIPTION_SUMMARY" "the description reaches CPack"
  assert_contains "$cmake" "CPACK_PACKAGE_CONTACT" "the contact reaches CPack"

  # Runtime dependencies for the package formats.
  assert_exit 0 "publishing a library succeeds" forge_in "$lib" publish --format TGZ

  local tarball listing out
  tarball="$(find "$lib/dist" -name '*.tar.gz' | head -1 || true)"
  if [[ -n "$tarball" ]]; then
    pass "a tarball is produced"
  else
    fail "a tarball is produced"
    return
  fi

  listing="$(tar -tzf "$tarball")"
  if [[ "$listing" == *"include/demo.hpp"* && "$listing" == *"lib/libdemo_pub.a"* ]]; then
    pass "the package contains the headers and the library"
  else
    fail "the package contains the headers and the library (got '$(flatten <<<"$listing" | head -c 200)')"
  fi
  if [[ "$listing" == *"lib/cmake/demo_pub/demo_pubConfig.cmake"* ]]; then
    pass "the CMake package travels with the archive"
  else
    fail "the CMake package travels with the archive"
  fi

  # Packaging an existing build must not rebuild it, and republishing the same
  # archive must still report it.
  assert_exit 0 "publish --no-build reuses the existing build" \
    forge_in "$lib" publish --format TGZ --no-build
  out="$(forge_in "$lib" publish --format TGZ --no-build 2>&1 || true)"
  if grep -qF "demo_pub-1.2.3-Linux.tar.gz" <<<"$(flatten <<<"$out")"; then
    pass "the produced archive is reported"
  else
    fail "the produced archive is reported"
  fi

  # --- a versioned executable ----------------------------------------------
  local app="$base/app"
  mkdir -p "$app/src"
  make_plain_project "$app" demo_app_pub
  sed_in_place 's/standard = "20"/standard = "20", version = "0.1.0"/' "$app/forge.lua"
  printf '#include <cstdio>\nint main() { std::printf("hi\\n"); return 0; }\n' >"$app/src/main.cpp"

  assert_exit 0 "publishing an executable succeeds" forge_in "$app" publish --format ZIP
  local zip
  zip="$(find "$app/dist" -name '*.zip' | head -1 || true)"
  if [[ -n "$zip" ]]; then
    pass "a zip is produced"
  else
    fail "a zip is produced"
  fi
  if command -v unzip >/dev/null 2>&1; then
    if unzip -l "$zip" | grep -qF "bin/demo_app_pub"; then
      pass "the executable is packaged under bin/"
    else
      fail "the executable is packaged under bin/"
    fi
  else
    skip "unzip is not installed"
  fi

  # --- a Debian package -----------------------------------------------------
  if command -v ar >/dev/null 2>&1; then
    assert_exit 0 "publishing a .deb succeeds" forge_in "$lib" publish --format DEB --output deb
    local deb
    deb="$(find "$lib/deb" -name '*.deb' | head -1 || true)"
    if [[ -n "$deb" && "$(ar t "$deb" | tr '\n' ' ')" == *"debian-binary"* ]]; then
      pass "the .deb is a real Debian package"
    else
      fail "the .deb is a real Debian package"
    fi
  else
    skip "ar is not installed"
  fi

  # --- runtime dependencies of the packages ---------------------------------
  # One line on purpose: `\n` in a sed replacement is a GNU extension.
  sed_in_place 's/install_headers = true/install_headers = true, package_depends = { "libstdc++6", deb = { "libdemo (>= 1.0)" }, rpm = { "demo >= 1.0" } }/' \
    "$lib/forge.lua"
  assert_exit 0 "a package with runtime dependencies builds" forge_in "$lib" build
  assert_contains "$lib/.config/cmake/CMakeLists.txt" \
    'set(CPACK_DEBIAN_PACKAGE_DEPENDS "libstdc++6, libdemo (>= 1.0)")' \
    "the DEB dependencies are emitted"
  assert_contains "$lib/.config/cmake/CMakeLists.txt" \
    'set(CPACK_RPM_PACKAGE_REQUIRES "libstdc++6, demo >= 1.0")' \
    "the RPM dependencies are emitted"

  local deb_out
  deb_out="$(forge_in "$lib" publish --format DEB --output deb-deps --no-build 2>&1 || true)"
  if grep -qF "CPACK_DEBIAN_PACKAGE_DEPENDS not set" <<<"$(flatten <<<"$deb_out")"; then
    fail "CPack no longer warns about missing dependencies"
  else
    pass "CPack no longer warns about missing dependencies"
  fi

  # A config rewrite keeps them.
  forge_in "$lib" add other --path ../plain >/dev/null 2>&1 || true
  assert_contains "$lib/forge.lua" "package_depends" "a config rewrite keeps the package dependencies"

  # --- failure paths --------------------------------------------------------
  local plain="$base/plain"
  mkdir -p "$plain/src"
  make_plain_project "$plain" demo_plain
  out="$(forge_in "$plain" publish 2>&1 || true)"
  if grep -qF "project.version" <<<"$(flatten <<<"$out")"; then
    pass "publishing without a version explains what is missing"
  else
    fail "publishing without a version explains what is missing"
  fi

  local fresh="$base/fresh"
  mkdir -p "$fresh/src"
  make_plain_project "$fresh" demo_fresh
  sed_in_place 's/standard = "20"/standard = "20", version = "9.9.9"/' "$fresh/forge.lua"
  assert_exit 1 "publishing --no-build without a build fails" \
    forge_in "$fresh" publish --no-build
  out="$(forge_in "$fresh" publish --no-build 2>&1 || true)"
  if grep -qF "CPackConfig.cmake" <<<"$(flatten <<<"$out")"; then
    pass "the missing CPack configuration is named"
  else
    fail "the missing CPack configuration is named"
  fi

  # An unknown generator is reported by cpack, not swallowed.
  if out="$(forge_in "$app" publish --format NOPE --no-build 2>&1)"; then
    fail "an unknown package format fails"
  else
    pass "an unknown package format fails"
  fi
}
