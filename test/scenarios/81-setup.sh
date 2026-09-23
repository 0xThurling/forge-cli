# `forge setup`: report the tools Forge uses, reject unknown names, and print an
# install plan without running it. `forge doctor` reports the same table.
scenario_81_setup() {
  local out flat

  # --- the check ------------------------------------------------------------
  if out="$(forge setup --tools cmake,git 2>&1)"; then
    pass "present required tools exit 0"
  else
    fail "present required tools exit 0"
  fi

  flat="$(flatten <<<"$out")"
  local needle
  for needle in "Tools Forge uses" "cmake" "git" "ok"; do
    if grep -qF "$needle" <<<"$flat"; then
      pass "setup reports $needle"
    else
      fail "setup reports $needle"
    fi
  done

  # --- json ----------------------------------------------------------------
  out="$(forge setup --json --tools cmake,git 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF '"tool":"cmake"' <<<"$flat" && grep -qF '"installed":true' <<<"$flat"; then
    pass "setup --json reports the tools"
  else
    fail "setup --json reports the tools (got '${flat:0:160}')"
  fi
  if grep -qF '"packageManager"' <<<"$flat"; then
    pass "setup --json names the package manager"
  else
    fail "setup --json names the package manager"
  fi

  # --- an unknown tool ------------------------------------------------------
  assert_exit 1 "an unknown tool is rejected" forge setup --tools definitely-not-a-tool
  out="$(forge setup --tools definitely-not-a-tool 2>&1 || true)"
  if grep -qF "unknown tool" <<<"$(flatten <<<"$out")"; then
    pass "the unknown tool is named"
  else
    fail "the unknown tool is named"
  fi

  # --- the install plan -----------------------------------------------------
  out="$(forge setup --install --dry-run --tools cmake,ninja 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "No known package manager" <<<"$flat"; then
    skip "no known package manager to plan with"
  else
    if grep -qE "(apt-get|dnf|zypper|apk|pacman|brew|winget|choco)" <<<"$flat" &&
      grep -qF "install" <<<"$flat"; then
      pass "the plan names the install command"
    else
      fail "the plan names the install command (got '${flat:0:200}')"
    fi

    if grep -qF -- "--dry-run: nothing was run" <<<"$flat"; then
      pass "the dry run changes nothing"
    else
      fail "the dry run changes nothing"
    fi
  fi

  # --- doctor uses the same table ------------------------------------------
  local root="$WORK/81-setup/project"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_setup
  out="$(forge_in "$root" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "9. Toolchain" <<<"$flat"; then
    pass "doctor reports the toolchain"
  else
    fail "doctor reports the toolchain"
  fi
  if grep -qF "cmake -" <<<"$flat"; then
    pass "doctor reports tool versions"
  else
    fail "doctor reports tool versions"
  fi
}
