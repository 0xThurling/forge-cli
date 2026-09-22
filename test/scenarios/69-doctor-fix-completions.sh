# `forge doctor --fix` repairs a project layout, and `forge completions`
# prints usable completion scripts.
scenario_69_doctor_fix_completions() {
  local root="$WORK/69-doctor-fix-completions"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_repair

  # --- doctor --fix ----------------------------------------------------------
  local out flat
  out="$(forge_in "$root" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF ".config/forge/ - Forge configuration (missing)" <<<"$flat"; then
    pass "doctor reports the missing directory"
  else
    fail "doctor reports the missing directory"
  fi

  out="$(forge_in "$root" doctor --fix 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF ".config/forge/ - Forge configuration (created)" <<<"$flat"; then
    pass "doctor --fix reports what it created"
  else
    fail "doctor --fix reports what it created"
  fi

  for path in assets external .config/forge .config/forge/build; do
    assert_exists "$root/$path" "doctor --fix created $path"
  done

  out="$(forge_in "$root" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Project looks healthy" <<<"$flat"; then
    pass "the repaired project is healthy"
  else
    fail "the repaired project is healthy"
  fi

  # --- completions -----------------------------------------------------------
  out="$(forge completions bash 2>&1)"
  if bash -n <(printf '%s\n' "$out") 2>/dev/null; then
    pass "the bash completion script is valid"
  else
    fail "the bash completion script is valid"
  fi
  if [[ "$out" == *"complete -F _forge forge"* && "$out" == *"build clean"* &&
    "$out" == *"project"* && "$out" == *"struct"* ]]; then
    pass "the bash script covers commands and subcommands"
  else
    fail "the bash script covers commands and subcommands"
  fi

  out="$(forge completions zsh 2>&1)"
  if [[ "$out" == *"#compdef forge"* && "$out" == *"commands=("* ]]; then
    pass "the zsh script is emitted"
  else
    fail "the zsh script is emitted"
  fi

  out="$(forge completions fish 2>&1)"
  if [[ "$out" == *"complete -c forge"* ]]; then
    pass "the fish script is emitted"
  else
    fail "the fish script is emitted"
  fi

  assert_exit 1 "an unsupported shell fails" forge completions powershell
  out="$(forge completions powershell 2>&1 || true)"
  if grep -qF "unsupported shell" <<<"$(flatten <<<"$out")"; then
    pass "the unsupported shell is reported"
  else
    fail "the unsupported shell is reported"
  fi
}
