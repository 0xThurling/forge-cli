# Custom scripts are listed and can be executed with `forge run <name>`.
scenario_23_scripts() {
  local root="$WORK/23-scripts"
  make_plain_project "$root" demo_scripts
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_scripts", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {
    hello = "echo hello-from-script",
    fail = "exit 3"
  },
  features = {}
}
LUA

  local out flat
  out="$(forge_in "$root" project scripts 2>&1)"
  flat="$(flatten <<<"$out")"
  if [[ "$out" == *hello* ]]; then
    pass "lists the configured script"
  else
    fail "lists the configured script"
  fi
  if grep -qF "package.toml" <<<"$flat"; then
    fail "does not mention package.toml"
  else
    pass "does not mention package.toml"
  fi

  out="$(forge_in "$root" run hello 2>&1 || true)"
  if [[ "$out" == *hello-from-script* ]]; then
    pass "runs the script"
  else
    fail "runs the script (got '$(flatten <<<"$out")')"
  fi

  assert_exit 3 "propagates a failing script's exit code" \
    forge_in "$root" run fail
}
