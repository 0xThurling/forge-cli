# `forge.get_packages`: the package manager invocation, the sudo path with a
# password, and the error when the manager is unknown.
scenario_50_get_packages() {
  local root="$WORK/50-get-packages"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_pkgs

  cat >"$bin/pacman" <<'STUB'
#!/usr/bin/env bash
echo "pacman $*" >>"${PKG_LOG:-/dev/null}"
STUB
  cat >"$bin/sudo" <<'STUB'
#!/usr/bin/env bash
{
  echo "sudo $*"
  if read -r line; then echo "stdin: $line"; fi
} >>"${SUDO_LOG:-/dev/null}"
STUB
  chmod +x "$bin/pacman" "$bin/sudo"

  cat >"$root/.config/forge/build/pkgs.lua" <<'LUA'
-- No password needed: the manager is invoked directly.
forge.get_packages("nopass", "pacman", { "vulkan-headers", "vulkan-icd-loader" })
-- With a password: sudo, with the password on stdin.
forge.get_packages("hunter2", "pacman", { "extra-pkg" })
return {}
LUA

  local old_path="$PATH"
  export PATH="$bin:$PATH"
  export PKG_LOG="$root/pacman.log"
  export SUDO_LOG="$root/sudo.log"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with get_packages"
    export PATH="$old_path"
    return
  fi
  pass "builds with get_packages"

  assert_contains "$root/pacman.log" "pacman -S vulkan-headers vulkan-icd-loader --noconfirm" \
    "the manager is invoked without sudo when nopass"
  assert_contains "$root/sudo.log" "sudo -S pacman -S extra-pkg --noconfirm" \
    "a password routes through sudo"
  assert_contains "$root/sudo.log" "stdin: hunter2" "the password is written to stdin"

  # An unknown manager is a script error: the build fails cleanly.
  cat >"$root/.config/forge/build/pkgs.lua" <<'LUA'
forge.get_packages("nopass", "no-such-manager", { "x" })
return {}
LUA
  assert_exit 1 "an unknown manager fails the build" forge_in "$root" build

  export PATH="$old_path"
}
