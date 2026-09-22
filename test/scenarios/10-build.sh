# A minimal project builds, generates its CMake, and runs.
scenario_10_build() {
  local root="$WORK/10-build"
  make_plain_project "$root" demo_build

  local out
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds a minimal project"
    tail -5 <<<"$out"
    return
  fi
  pass "builds a minimal project"

  assert_contains "$root/CMakeLists.txt" 'include(.config/cmake/CMakeLists.txt)' \
    "root CMakeLists generated"
  assert_exists "$root/.config/cmake/CMakeLists.txt" "generated CMake config"
  assert_exists "$root/compile_commands.json" "compile_commands.json symlinked for the LSP"
  assert_runs "$root/build/demo_build" "hello from demo_build" "binary runs"

  # Verbose mode streams CMake's own output.
  local verbose_out
  verbose_out="$(forge_in "$root" build -v 2>&1 || true)"
  if grep -qE "Building CXX object|cmake --build|Build finished" <<<"$verbose_out"; then
    pass "verbose build streams CMake output"
  else
    fail "verbose build streams CMake output"
  fi

  # A rebuild of an unchanged project is still a success.
  assert_exit 0 "rebuild is clean" forge_in "$root" build
}
