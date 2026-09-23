# `version_from_git`: the version comes from the newest tag, stays numeric for
# project(VERSION ...), counts commits after the tag, and falls back cleanly on
# a repository with no version-like tag.
scenario_82_version_from_git() {
  local root="$WORK/82-version-from-git"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_version
  sed_in_place 's/standard = "20"/standard = "20", version_from_git = true/' "$root/forge.lua"

  git -C "$root" init -q -b main
  git -C "$root" add -A
  git -C "$root" -c user.email=e2e@test -c user.name=e2e commit -qm init

  # No tag yet: nothing to derive, so the project stays unversioned.
  local out
  out="$(forge_in "$root" build 2>&1 || true)"
  if grep -qF "no version-like tag" <<<"$(flatten <<<"$out")"; then
    pass "a repository without tags is reported"
  else
    fail "a repository without tags is reported"
  fi
  # (`cmake_minimum_required(VERSION …)` is unrelated: check the project() line)
  assert_lacks "$root/CMakeLists.txt" "project(demo_version VERSION" "no version is invented"

  # A tag becomes the version.
  git -C "$root" tag v2.5.0
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/CMakeLists.txt" "project(demo_version VERSION 2.5.0 LANGUAGES CXX C)" \
    "the tag becomes the project version"

  # Commits after the tag are a fourth component, so the version stays numeric
  # (project(... VERSION ...) and SOVERSION both require that).
  printf '// a change after the tag\n' >>"$root/src/main.cpp"
  git -C "$root" add -A
  git -C "$root" -c user.email=e2e@test -c user.name=e2e commit -qm change
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/CMakeLists.txt" "project(demo_version VERSION 2.5.0.1 LANGUAGES CXX C)" \
    "commits after the tag extend the version"

  # A version-like tag further back is used when HEAD is untagged.
  git -C "$root" tag v2.6.0
  printf '// another\n' >>"$root/src/main.cpp"
  git -C "$root" add -A
  git -C "$root" -c user.email=e2e@test -c user.name=e2e commit -qm another
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/CMakeLists.txt" "VERSION 2.6.0.1" "the newest tag wins"
}
