# A local `path` dependency: SOURCE_DIR emitted, nothing fetched, links and runs.
scenario_11_path_dep() {
  local root="$WORK/11-path-dep"
  make_dep_project "$root"

  if ! forge_in "$root/app" build >/dev/null 2>&1; then
    fail "builds with a path dependency"
    return
  fi
  pass "builds with a path dependency"

  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    'FetchContent_Declare(demo SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../lib")' \
    "emits SOURCE_DIR for the local checkout"
  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    'target_link_libraries(demo_app PRIVATE demo_lib)' \
    "links the declared target"
  assert_missing "$root/app/build/_deps/demo-src" "nothing is fetched"
  assert_runs "$root/app/build/demo_app" "demo_value=42" "runs against the local library"
}
