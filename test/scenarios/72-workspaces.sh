# Workspaces: discovery, dependency ordering, and building sibling projects.
scenario_72_workspaces() {
  local ws="$WORK/72-workspaces/ws"
  mkdir -p "$ws/fp/src" "$ws/ml/src" "$ws/app/src"

  # fp: a library.
  cat >"$ws/fp/forge.lua" <<'LUA'
return {
  project = { name = "forgefp", type = "library", version = "1.0.0", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int fp_value() { return 41; }\n' >"$ws/fp/src/fp.cpp"

  # ml: a library on top of fp.
  cat >"$ws/ml/forge.lua" <<'LUA'
return {
  project = { name = "forgeml", type = "library", version = "0.2.0", standard = "20" },
  dependencies = {
    direct = {
      forgefp = { path = "../fp", target = "forgefp" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int fp_value();\nint ml_value() { return fp_value() + 1; }\n' >"$ws/ml/src/ml.cpp"

  # app: an executable on top of ml.
  cat >"$ws/app/forge.lua" <<'LUA'
return {
  project = { name = "demoapp", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      forgeml = { path = "../ml", target = "forgeml" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint ml_value();\nint main() { std::printf("%%d\\n", ml_value()); return 0; }\n' \
    >"$ws/app/src/main.cpp"

  # --- discovery ------------------------------------------------------------
  local out flat
  out="$(forge_in "$ws" workspace list 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  assert_order "$flat" "the workspace lists the projects in build order" \
    "forgefp" "forgeml" "demoapp"
  if [[ "$flat" == *"1.0.0"* && "$flat" == *"0.2.0"* ]]; then
    pass "versions are shown"
  else
    fail "versions are shown"
  fi
  if [[ "$flat" == *"forgeml"* && "$flat" == *"forgefp"* ]]; then
    pass "local dependencies are detected"
  else
    fail "local dependencies are detected"
  fi

  # --json gives the same information to a tool.
  out="$(forge_in "$ws" workspace list --json 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF '"name":"forgefp"' <<<"$flat" && grep -qF '"dependsOn":["forgefp"]' <<<"$flat"; then
    pass "workspace list --json includes the graph"
  else
    fail "workspace list --json includes the graph (got '${flat:0:160}')"
  fi

  # The same workspace is found from inside a member project.
  out="$(forge_in "$ws/ml" workspace list 2>&1 || true)"
  assert_order "$(flatten <<<"$out")" "the workspace is found from a member project" \
    "forgefp" "forgeml" "demoapp"

  # --- build ----------------------------------------------------------------
  out="$(forge_in "$ws" workspace build --dry-run 2>&1 || true)"
  assert_order "$(flatten <<<"$out")" "the dry run prints the build order" \
    "forgefp" "forgeml" "demoapp"

  if ! forge_in "$ws" workspace build >/dev/null 2>&1; then
    fail "the workspace builds"
  else
    pass "the workspace builds"
  fi
  assert_runs "$ws/app/build/demoapp" "42" "the app links against both libraries"

  # Selecting a project pulls in what it depends on (and the name is matched
  # case-insensitively, so the CLI spelling does not have to match forge.lua).
  out="$(forge_in "$ws" workspace build DemoApp --dry-run 2>&1 || true)"
  assert_order "$(flatten <<<"$out")" "selecting a project includes its dependencies" \
    "forgefp" "forgeml" "demoapp"

  assert_exit 1 "an unknown project is rejected" forge_in "$ws" workspace build nope
  out="$(forge_in "$ws" workspace build nope 2>&1 || true)"
  if grep -qF "unknown project" <<<"$(flatten <<<"$out")"; then
    pass "the unknown project is named"
  else
    fail "the unknown project is named"
  fi

  # --- test -----------------------------------------------------------------
  out="$(forge_in "$ws" workspace test --dry-run 2>&1 || true)"
  assert_order "$(flatten <<<"$out")" "the test order matches the build order" \
    "forgefp" "forgeml" "demoapp"

  # A project that cannot build must stop the run and be named.
  mkdir -p "$ws/bad/src"
  cat >"$ws/bad/forge.lua" <<'LUA'
return {
  project = { name = "bad", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int main() { this is not C++ }\n' >"$ws/bad/src/main.cpp"

  if out="$(forge_in "$ws" workspace test bad 2>&1)"; then
    fail "a failing project fails the workspace run"
  else
    pass "a failing project fails the workspace run"
  fi
  if grep -qF "failed in bad" <<<"$(flatten <<<"$out")"; then
    pass "the failing project is named"
  else
    fail "the failing project is named"
  fi

  # --- an explicit workspace file ------------------------------------------
  cat >"$ws/forge.workspace.lua" <<'LUA'
return {
  name = "forge",
  projects = { "fp", "app" }
}
LUA
  out="$(forge_in "$ws" workspace list 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if [[ "$flat" == *"forgefp"* && "$flat" == *"demoapp"* && "$flat" != *"forgeml"* ]]; then
    pass "an explicit project list is honoured"
  else
    fail "an explicit project list is honoured (got '$flat')"
  fi
}
