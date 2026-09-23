# The Lua environment tables: forge.os, forge.distro, forge.package_manager and
# forge.current_working_dir — what a setup script branches on.
scenario_53_lua_environment() {
  local root="$WORK/53-lua-environment"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_env

  cat >"$root/.config/forge/build/env.lua" <<'LUA'
forge.log.info("os = " .. forge.os.current)
forge.log.info("is_linux = " .. tostring(forge.os.current == forge.os.linux))
forge.log.info("distro = " .. forge.distro.my_distro)
forge.log.info("pacman = " .. forge.package_manager.pacman)
forge.log.info("nopass = " .. forge.package_manager.no_pass)
forge.log.info("aptget = " .. forge.package_manager.aptget)
forge.log.info("cwd = " .. forge.current_working_dir)
return {}
LUA

  local out flat
  out="$(forge_in "$root" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"

  if [[ "$(uname -s)" == "Linux" ]]; then
    if grep -qF "os = linux" <<<"$flat" && grep -qF "is_linux = true" <<<"$flat"; then
      pass "forge.os reports the running platform"
    else
      fail "forge.os reports the running platform"
    fi
  else
    skip "not Linux, so the os constants are not asserted"
  fi

  # The distro is one of the published constants.
  if grep -qE "distro = (nixos|fedora|manjaro|arch|ubuntu|debian|redhat|unknown)" <<<"$flat"; then
    pass "forge.distro.my_distro is a known constant"
  else
    fail "forge.distro.my_distro is a known constant"
  fi

  if grep -qF "pacman = pacman" <<<"$flat" &&
    grep -qF "nopass = nopass" <<<"$flat" &&
    grep -qF "aptget = apt-get" <<<"$flat"; then
    pass "forge.package_manager constants"
  else
    fail "forge.package_manager constants"
  fi

  # Scripts run from the project root (also when forge was started elsewhere).
  if grep -qF "cwd = $root" <<<"$flat"; then
    pass "forge.current_working_dir is the project root"
  else
    fail "forge.current_working_dir is the project root"
  fi
}
