# The `build` section: documented keys and their accepted aliases.
scenario_52_build_flags_aliases() {
  local base="$WORK/52-build-flags-aliases"

  # Documented names.
  local doc="$base/documented"
  make_plain_project "$doc" demo_doc
  cat >"$doc/forge.lua" <<'LUA'
return {
  project = { name = "demo_doc", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = {
    cxx_flags = { "-DDOC_FLAG=1" },
    link_flags = { "-rdynamic" },
    compile_definitions = { "DOC_DEFINE=1" },
    link_libraries = { "m" }
  }
}
LUA
  forge_in "$doc" build >/dev/null 2>&1 || true
  local cmake="$doc/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'add_compile_options("-DDOC_FLAG=1")' "cxx_flags emitted"
  assert_contains "$cmake" 'add_link_options("-rdynamic")' "link_flags emitted"
  assert_contains "$cmake" "add_compile_definitions(DOC_DEFINE=1)" "compile_definitions emitted"
  assert_contains "$cmake" "link_libraries(m)" "link_libraries emitted"

  # The earlier spellings still work.
  local alias="$base/aliases"
  make_plain_project "$alias" demo_alias
  cat >"$alias/forge.lua" <<'LUA'
return {
  project = { name = "demo_alias", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = {
    compile_options = { "-DALIAS_FLAG=1" },
    link_options = { "-Wl,--as-needed" },
    definitions = { "ALIAS_DEFINE=1" }
  }
}
LUA
  forge_in "$alias" build >/dev/null 2>&1 || true
  cmake="$alias/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'add_compile_options("-DALIAS_FLAG=1")' "compile_options alias works"
  assert_contains "$cmake" 'add_link_options("-Wl,--as-needed")' "link_options alias works"
  assert_contains "$cmake" "add_compile_definitions(ALIAS_DEFINE=1)" "definitions alias works"
}
