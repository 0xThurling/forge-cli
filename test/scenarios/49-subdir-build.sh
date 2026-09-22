# Building from a subdirectory works (the project root is found by walking up),
# and a stray nested build directory does not leak into the source glob.
scenario_49_subdir_build() {
  local root="$WORK/49-subdir-build"
  mkdir -p "$root/src/nested"
  make_plain_project "$root" demo_subdir
  printf 'int helper() { return 1; }\n' >"$root/src/nested/helper.cpp"

  # Build from src/ rather than the project root.
  if (cd "$root/src" && "${FORGE_CMD[@]}" build >/dev/null 2>&1); then
    pass "builds from a subdirectory"
  else
    fail "builds from a subdirectory"
  fi
  assert_exists "$root/build/demo_subdir" "the binary lands in the project root"
  assert_runs "$root/build/demo_subdir" "hello from demo_subdir" "the binary runs"

  # A nested build directory (src/build) must not contribute sources: a stray
  # main() there would break the link.
  mkdir -p "$root/src/build"
  printf 'int main() { return 0; }\n' >"$root/src/build/stray.cpp"
  if forge_in "$root" build >/dev/null 2>&1; then
    pass "a nested build directory is excluded from the sources"
  else
    fail "a nested build directory is excluded from the sources"
  fi
  assert_runs "$root/build/demo_subdir" "hello from demo_subdir" "the real binary still runs"

  # The same filter applies to the test target's globs.
  assert_contains "$root/.config/cmake/CMakeLists.txt" \
    'list(FILTER SOURCES EXCLUDE REGEX "/(build|build-[^/]*|CMakeFiles)/")' \
    "source glob filters build directories"
}
