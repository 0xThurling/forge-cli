# The `project` subcommands answer without failing, and use the current
# configuration file name in their messages.
scenario_22_project_commands() {
  local root="$WORK/22-project-commands"
  make_plain_project "$root" demo_proj

  local out flat
  for cmd in info tree stats dependencies scripts; do
    if out="$(forge_in "$root" project "$cmd" 2>&1)"; then
      pass "project $cmd"
    else
      fail "project $cmd"
      continue
    fi

    flat="$(flatten <<<"$out")"
    if grep -qF "package.toml" <<<"$flat"; then
      fail "project $cmd does not mention package.toml"
    else
      pass "project $cmd does not mention package.toml"
    fi
  done

  out="$(forge_in "$root" project info 2>&1)"
  if [[ "$out" == *demo_proj* ]]; then
    pass "project info names the project"
  else
    fail "project info names the project"
  fi

  out="$(forge_in "$root" project stats 2>&1)"
  if [[ "$out" == *"Total Files"* ]]; then
    pass "project stats reports totals"
  else
    fail "project stats reports totals"
  fi
}
