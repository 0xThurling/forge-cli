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
  # The checkout comes either from build/_deps (a plain fetch) or from the
  # shared cache, which is what FetchContent is pointed at when it is warm.
  if [[ -d "$root/app/build/_deps/demo-src" ]] ||
    find "${FORGE_CACHE_DIR:-$HOME/.cache/forge/deps}" -maxdepth 1 -name "demo-*" 2>/dev/null | grep -q .; then
    pass "fetches the repository"
  else
    fail "fetches the repository"
  fi
  assert_runs "$root/app/build/demo_app" "demo_value=42" "runs against the fetched library"
}
