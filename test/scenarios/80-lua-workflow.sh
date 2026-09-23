# The Lua workflow API: forge.exec captures a program's output, file and
# template helpers write relative to the project, forge.git reads state, and
# cmakeOptions.cacheVariables reaches CMake's cache.
scenario_80_lua_workflow() {
  local root="$WORK/80-lua-workflow"
  mkdir -p "$root/src" "$root/.config/forge/build" "$root/templates"
  make_plain_project "$root" demo_workflow

  # A repository, so forge.git.* has something to report.
  git -C "$root" init -q -b main
  git -C "$root" add -A
  git -C "$root" -c user.email=e2e@test -c user.name=e2e commit -qm init
  git -C "$root" tag v1.2.3

  cat >"$root/templates/version.h.in" <<'TPL'
#pragma once
#define APP_VERSION "@VERSION@"
#define APP_REVISION "@REVISION@"
TPL

  cat >"$root/.config/forge/build/workflow.lua" <<'LUA'
-- Run a program and read what it printed.
local code, output = forge.exec("echo exec-works")
forge.write_file("generated/exec.txt", code .. ":" .. output)

-- Files and templates, all relative to the project root.
forge.mkdir("generated")
forge.copy_file("src/main.cpp", "generated/main.cpp.bak")
forge.template("templates/version.h.in", "generated/version.h", {
  VERSION = forge.git.describe() or "unknown",
  REVISION = forge.git.rev() or "unknown",
})

-- The branch is read too, to prove the git table works end to end.
forge.write_file("generated/branch.txt", tostring(forge.git.branch() or "none"))

return {
  cmakeOptions = {
    cacheVariables = { WORKFLOW_OPTION = "from-lua" },
    includeDirs = { "generated" }
  }
}
LUA
  printf '#include <cstdio>\n#include <version.h>\nint main() { std::printf("%%s\\n", APP_VERSION); return 0; }\n' \
    >"$root/src/main.cpp"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "a project using the Lua workflow helpers builds"
    return
  fi
  pass "a project using the Lua workflow helpers builds"

  assert_contains "$root/generated/exec.txt" "0:exec-works" \
    "forge.exec returns the exit code and the output"
  assert_exists "$root/generated/main.cpp.bak" "forge.copy_file copies"
  assert_contains "$root/generated/version.h" '#define APP_REVISION "' \
    "forge.template renders placeholders"
  assert_contains "$root/generated/version.h" "v1.2.3" "forge.git.describe reports the tag"
  assert_contains "$root/generated/branch.txt" "main" "forge.git.branch reports the branch"

  # cmakeOptions.cacheVariables is a cache variable (not a plain set), so it is
  # visible to toolchains and dependencies.
  assert_contains "$root/.config/cmake/CMakeLists.txt" \
    'set(WORKFLOW_OPTION "from-lua" CACHE STRING "" FORCE)' \
    "cacheVariables are emitted as cache variables"
  assert_contains "$root/build/CMakeCache.txt" "WORKFLOW_OPTION:STRING=from-lua" \
    "the cache variable reaches CMake"

  local output
  output="$(timeout 60 "$root/build/demo_workflow" 2>&1 || true)"
  if grep -qF "v1.2.3" <<<"$output"; then
    pass "the generated header is compiled into the program"
  else
    fail "the generated header is compiled into the program (got '$output')"
  fi

  # The editor stubs are refreshed by `doctor --fix` and cover the new API.
  forge_in "$root" doctor --fix >/dev/null 2>&1 || true
  assert_contains "$root/.config/forge/definitions/definitions.lua" "forge.exec" \
    "the Lua stubs document forge.exec"
  assert_contains "$root/.config/forge/definitions/definitions.lua" "forge.git.describe" \
    "the Lua stubs document forge.git"
  assert_contains "$root/.config/forge/definitions/definitions.lua" "forge.add_section" \
    "the Lua stubs document forge.add_section"
}
