# `scripts`: pre-build and post-build hooks run around the build, and a failing
# pre-build stops it.
scenario_47_build_hooks() {
  local root="$WORK/47-build-hooks"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_hooks
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_hooks", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {
    ["pre-build"] = "echo pre > pre.txt",
    ["post-build"] = "echo post > post.txt"
  },
  features = {}
}
LUA

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with build hooks"
    return
  fi
  pass "builds with build hooks"
  assert_exists "$root/pre.txt" "pre-build ran"
  assert_exists "$root/post.txt" "post-build ran"

  # A failing pre-build stops the build before CMake runs.
  rm -f "$root/pre.txt" "$root/post.txt"
  rm -rf "$root/build"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_hooks", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = { ["pre-build"] = "exit 7" },
  features = {}
}
LUA
  assert_exit 1 "a failing pre-build fails the build" forge_in "$root" build
  assert_missing "$root/build" "a failing pre-build stops before CMake"
  assert_missing "$root/post.txt" "post-build does not run"
}
