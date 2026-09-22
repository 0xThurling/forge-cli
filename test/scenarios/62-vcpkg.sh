# vcpkg (manifest mode): vcpkg.json generation, the toolchain hand-off, the
# find_package/link wiring, and the error paths.
scenario_62_vcpkg() {
  local root="$WORK/62-vcpkg"
  local fake="$root/fake-vcpkg"
  local app="$root/app"

  # A fake vcpkg checkout: the toolchain file, plus a package config for fmt so
  # `find_package(fmt)` succeeds without installing anything.
  mkdir -p "$fake/scripts/buildsystems" "$fake/installed/x64-linux/share/fmt" \
    "$fake/installed/x64-linux/share/spdlog"
  cat >"$fake/scripts/buildsystems/vcpkg.cmake" <<CMAKE
set(VCPKG_STUB_TOOLCHAIN ON CACHE BOOL "stub vcpkg toolchain" FORCE)
list(APPEND CMAKE_PREFIX_PATH "$fake/installed/x64-linux/share/fmt")
list(APPEND CMAKE_PREFIX_PATH "$fake/installed/x64-linux/share/spdlog")
CMAKE
  for package in fmt spdlog; do
    cat >"$fake/installed/x64-linux/share/$package/$package-config.cmake" <<CMAKE
if(NOT TARGET $package::$package)
  add_library($package::$package INTERFACE IMPORTED)
endif()
CMAKE
  done

  mkdir -p "$app/src"
  make_plain_project "$app" demo_vcpkg
  cat >"$app/forge.lua" <<LUA
return {
  project = { name = "demo_vcpkg", type = "executable", standard = "20", version = "0.4.0" },
  dependencies = {
    direct = {},
    conan = {},
    vcpkg = {
      fmt = "fmt::fmt",
      spdlog = { target = "spdlog::spdlog", version = "1.12.0" }
    }
  },
  resources = { files = {} },
  scripts = {},
  features = {},
  vcpkg_root = "$fake",
  vcpkg_baseline = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2"
}
LUA
  printf 'int main() { return 0; }\n' >"$app/src/main.cpp"

  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "builds with vcpkg dependencies"
    return
  fi
  pass "builds with vcpkg dependencies"

  # vcpkg.json
  assert_exists "$app/vcpkg.json" "vcpkg.json written"
  assert_contains "$app/vcpkg.json" '"name": "demo-vcpkg"' "package name sanitized"
  assert_contains "$app/vcpkg.json" '"version-string": "0.4.0"' "project version used"
  assert_contains "$app/vcpkg.json" '"builtin-baseline"' "baseline recorded"
  assert_contains "$app/vcpkg.json" '"fmt"' "declared package listed"
  assert_contains "$app/vcpkg.json" '"version>=": "1.12.0"' "minimum version recorded"

  # CMake wiring
  local cmake="$app/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "find_package(fmt REQUIRED)" "find_package emitted"
  assert_contains "$cmake" "find_package(spdlog REQUIRED)" "second package emitted"
  local link
  link="$(grep -m1 'target_link_libraries' "$cmake")"
  if [[ "$link" == *fmt::fmt* && "$link" == *spdlog::spdlog* ]]; then
    pass "vcpkg targets reach the link line"
  else
    fail "vcpkg targets reach the link line (got '$link')"
  fi
  assert_contains "$app/build/CMakeCache.txt" "VCPKG_STUB_TOOLCHAIN:BOOL=ON" \
    "the vcpkg toolchain was read by CMake"

  # Without a usable vcpkg checkout the build explains what to do.
  local missing="$root/missing"
  mkdir -p "$missing/src"
  make_plain_project "$missing" demo_novcpkg
  cat >"$missing/forge.lua" <<'LUA'
return {
  project = { name = "demo_novcpkg", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {}, vcpkg = { fmt = "fmt::fmt" } },
  resources = { files = {} },
  scripts = {},
  features = {},
  vcpkg_root = "/nonexistent/vcpkg"
}
LUA
  printf 'int main() { return 0; }\n' >"$missing/src/main.cpp"
  assert_exit 1 "a missing vcpkg checkout fails the build" forge_in "$missing" build
  local out flat
  out="$(forge_in "$missing" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "no vcpkg checkout was found" <<<"$flat"; then
    pass "the missing checkout is explained"
  else
    fail "the missing checkout is explained"
  fi
}
