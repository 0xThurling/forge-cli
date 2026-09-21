# A git dependency still fetches (from a local file:// repo) and links.
scenario_13_git_dep() {
  local root="$WORK/13-git-dep"
  make_dep_project "$root"
  rm -rf "$root/app/build" "$root/app/.config" "$root/app/CMakeLists.txt"

  git -C "$root/lib" init -q
  git -C "$root/lib" add -A
  git -C "$root/lib" -c user.email=e2e@forge -c user.name=e2e commit -qm init
  git -C "$root/lib" tag v1

  cat >"$root/app/forge.lua" <<LUA
return {
  project = { name = "demo_app", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      demo = { git = "file://$root/lib", tag = "v1", target = "demo_lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA

  if ! forge_in "$root/app" build >/dev/null 2>&1; then
    fail "builds with a git dependency"
    return
  fi
  pass "builds with a git dependency"

  assert_contains "$root/app/.config/cmake/CMakeLists.txt" \
    "FetchContent_Declare(demo GIT_REPOSITORY \"file://$root/lib\" GIT_TAG \"v1\")" \
    "emits GIT_REPOSITORY/GIT_TAG"
  assert_exists "$root/app/build/_deps/demo-src" "fetches the repository"
  assert_runs "$root/app/build/demo_app" "demo_value=42" "runs against the fetched library"
}
