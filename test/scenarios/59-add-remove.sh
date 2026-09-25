# `forge add` / `forge remove`: mutate the dependency list from the CLI.
scenario_59_add_remove() {
  local root="$WORK/59-add-remove"
  local app="$root/app"
  local repo="$root/repo"

  # A git dependency and a local one to add.
  mkdir -p "$repo/include"
  cat >"$repo/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(gitdep LANGUAGES CXX)
add_library(gitdep INTERFACE)
target_include_directories(gitdep INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int from_git() { return 1; }\n' >"$repo/include/gitdep.h"
  git -C "$repo" init -q
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@forge -c user.name=e2e commit -qm init
  git -C "$repo" tag v1

  mkdir -p "$app/src" "$app/local/include"
  cat >"$app/local/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(localdep LANGUAGES CXX)
add_library(localdep INTERFACE)
target_include_directories(localdep INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
CMAKE
  printf '#pragma once\ninline int from_local() { return 2; }\n' >"$app/local/include/localdep.h"
  make_plain_project "$app" demo_add
  printf '#include <gitdep.h>\n#include <localdep.h>\nint main() { return from_git() + from_local(); }\n' \
    >"$app/src/main.cpp"

  # --- add ------------------------------------------------------------------
  assert_exit 0 "add a git dependency" \
    forge_in "$app" add gitdep --git "file://$repo" --tag v1 --target gitdep
  assert_exit 0 "add a path dependency" \
    forge_in "$app" add localdep --path local --target localdep
  assert_contains "$app/forge.lua" 'git = "file://'"$repo"'"' "the git dependency is written"
  assert_contains "$app/forge.lua" 'path = "local"' "the path dependency is written"

  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "a project with added dependencies builds"
    return
  fi
  pass "a project with added dependencies builds"

  # --- add errors -----------------------------------------------------------
  assert_exit 1 "add without a source fails" forge_in "$app" add nosource
  assert_exit 1 "add --git without --tag fails" \
    forge_in "$app" add notag --git "file://$repo"
  assert_exit 1 "add with a missing path fails" \
    forge_in "$app" add nopath --path does-not-exist
  assert_exit 1 "add with two sources fails" \
    forge_in "$app" add twosources --git "file://$repo" --tag v1 --conan 1.0.0

  # --- export metadata ------------------------------------------------------
  assert_exit 0 "add with export metadata" \
    forge_in "$app" add localdep --path local --target localdep \
      --export-package localdep --export-target localdep::localdep
  assert_contains "$app/forge.lua" 'package = "localdep"' "the export package is written"
  assert_contains "$app/forge.lua" 'target = "localdep::localdep"' "the export target is written"
  assert_exit 1 "add with only --export-package fails" \
    forge_in "$app" add half --path local --export-package localdep
  assert_lacks "$app/forge.lua" "half = {" "the incomplete export is not added"

  # --- lock -----------------------------------------------------------------
  forge_in "$app" install >/dev/null 2>&1 || true
  assert_contains "$app/forge.lock" '"gitdep"' "the added git dependency is locked"

  # --- switch channel (overwrite) -------------------------------------------
  assert_exit 0 "re-adding a name switches channel" \
    forge_in "$app" add gitdep --path local --target localdep
  assert_lacks "$app/forge.lua" "file://$repo" "the previous channel is replaced"

  # --- conan ----------------------------------------------------------------
  assert_exit 0 "add a conan dependency" forge_in "$app" add fmt --conan 10.2.1
  assert_contains "$app/forge.lua" 'fmt = "10.2.1"' "the conan dependency is written"
  local out flat
  out="$(forge_in "$app" project dependencies --json 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF '"channel":"conan"' <<<"$flat" && grep -qF '"name":"fmt"' <<<"$flat"; then
    pass "the conan dependency is listed"
  else
    fail "the conan dependency is listed"
  fi

  assert_exit 0 "remove the conan dependency" forge_in "$app" remove fmt
  assert_lacks "$app/forge.lua" 'fmt = "10.2.1"' "the conan dependency is gone"

  # --- remove ---------------------------------------------------------------
  assert_exit 0 "remove a git dependency" forge_in "$app" remove gitdep
  assert_lacks "$app/forge.lua" "gitdep" "the dependency is gone from forge.lua"
  assert_lacks "$app/forge.lock" '"gitdep"' "its lock entry is dropped"

  assert_exit 1 "removing an unknown dependency fails" forge_in "$app" remove nope
  out="$(forge_in "$app" remove nope 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "no dependency named" <<<"$flat"; then
    pass "the unknown name is reported"
  else
    fail "the unknown name is reported"
  fi

  # The project still builds with the remaining local dependency.
  printf '#include <localdep.h>\nint main() { return from_local(); }\n' >"$app/src/main.cpp"
  if forge_in "$app" build >/dev/null 2>&1; then
    pass "still builds after removals"
  else
    fail "still builds after removals"
  fi
}
