# The Lua API surface: logging, config get/set, repo cloning, package installs
# and the cmakeOptions diagnostics.
scenario_29_lua_api() {
  local root="$WORK/29-lua-api"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_api
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_api", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  custom = { lua_custom = "declared" }
}
LUA

  # A local git repo to clone with forge.pull_repo.
  local repo="$WORK/29-lua-api-repo"
  mkdir -p "$repo"
  printf '#pragma once\ninline int r() { return 1; }\n' >"$repo/r.hpp"
  git -C "$repo" init -q
  git -C "$repo" add -A
  git -C "$repo" -c user.email=e2e@forge -c user.name=e2e commit -qm init
  git -C "$repo" tag v1

  cat >"$root/.config/forge/build/api.lua" <<EOF
forge.log.info("info-from-script")
forge.log.warn("warn-from-script")
forge.log.error("error-from-script")

forge.pull_repo("file://$repo", "v1")

-- An unknown package manager must raise a catchable error, not kill the build.
local ok, err = pcall(function() forge.get_packages("nopass", "no-such-manager", { "x" }) end)
forge.log.info("get_packages caught = " .. tostring(not ok))

forge.log.info("custom = " .. tostring(forge.config.get("lua_custom")))
forge.log.info("missing = " .. tostring(forge.config.get("not_set") and "value" or "nil"))

return { cmakeOptions = { unknownKey = { "warn me" } } }
EOF

  local out flat
  out="$(forge_in "$root" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"

  for level in "INFO: info-from-script" "WARN: warn-from-script" "ERROR: error-from-script"; do
    if grep -qF "$level" <<<"$flat"; then
      pass "log.${level%%:*} output"
    else
      fail "log.${level%%:*} output"
    fi
  done

  if grep -qF "get_packages caught = true" <<<"$flat"; then
    pass "get_packages raises a catchable error"
  else
    fail "get_packages raises a catchable error"
  fi
  if grep -qF "custom = declared" <<<"$flat"; then
    pass "config.get reads the custom section"
  else
    fail "config.get reads the custom section"
  fi
  if grep -qF "missing = nil" <<<"$flat"; then
    pass "config.get of an unknown key is nil"
  else
    fail "config.get of an unknown key is nil"
  fi
  if grep -qF "unknown cmakeOptions key 'unknownKey'" <<<"$flat"; then
    pass "unknown cmakeOptions key warns"
  else
    fail "unknown cmakeOptions key warns"
  fi

  assert_exists "$root/external/29-lua-api-repo/r.hpp" "pull_repo clones into external/"
}
