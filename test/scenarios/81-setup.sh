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

  # --- the ecosystem hints name an installer -------------------------------
  if grep -qE "conan.*(pacman|apt-get|dnf|zypper|apk|brew|winget|choco|pipx)" <<<"$flat"; then
    pass "the conan hint names an installer for this machine"
  else
    fail "the conan hint names an installer for this machine (got '${flat:0:200}')"
  fi

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

  # --- doctor --json -------------------------------------------------------
  local root_json="$WORK/81-setup/json"
  mkdir -p "$root_json/src"
  make_plain_project "$root_json" demo_json
  out="$(forge_in "$root_json" doctor --json 2>&1 || true)"
  if python3 -c "import json,sys; d=json.loads(sys.argv[1]); sys.exit(0 if 'toolchain' in d and 'layout' in d else 1)" "$out" 2>/dev/null; then
    pass "doctor --json is valid JSON"
  else
    fail "doctor --json is valid JSON (got '${out:0:120}')"
  fi
  if [[ "$out" == "{"* ]]; then
    pass "doctor --json prints nothing else"
  else
    fail "doctor --json prints nothing else"
  fi

  # --- the ecosystem tools can be planned (and skipped) --------------------
  out="$(forge setup --install --dry-run --tools conan 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "Conan" <<<"$flat" &&
    grep -qE "(pacman|apt-get|dnf|zypper|apk|brew|winget|choco|pipx)" <<<"$flat"; then
    pass "conan has an install plan for this machine"
  else
    fail "conan has an install plan for this machine (got '${flat:0:200}')"
  fi

  out="$(forge setup --install --dry-run --tools vcpkg 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "microsoft/vcpkg" <<<"$flat" && grep -qF "bootstrap-vcpkg" <<<"$flat"; then
    pass "vcpkg has a clone-and-bootstrap plan"
  else
    fail "vcpkg has a clone-and-bootstrap plan (got '${flat:0:200}')"
  fi

  # A bootstrapped vcpkg in the project is reused as-is.
  local withvcpkg="$WORK/81-setup/with-vcpkg"
  mkdir -p "$withvcpkg/src" "$withvcpkg/external/vcpkg"
  make_plain_project "$withvcpkg" demo_vcpkg_present
  printf '#!/usr/bin/env bash
echo vcpkg-stub
' >"$withvcpkg/external/vcpkg/vcpkg"
  chmod +x "$withvcpkg/external/vcpkg/vcpkg"
  out="$(forge_in "$withvcpkg" setup --install --yes --tools vcpkg 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "already present" <<<"$flat"; then
    pass "a bootstrapped external/vcpkg is reused"
  else
    fail "a bootstrapped external/vcpkg is reused (got '${flat:0:200}')"
  fi

  # A checkout without the binary is bootstrapped rather than cloned again.
  local bare="$WORK/81-setup/bare-vcpkg"
  mkdir -p "$bare/src" "$bare/external/vcpkg"
  make_plain_project "$bare" demo_vcpkg_bare
  out="$(forge_in "$bare" setup --install --dry-run --tools vcpkg 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "bootstrap-vcpkg.sh" <<<"$flat" && ! grep -qF "git clone" <<<"$flat"; then
    pass "an unbootstrapped checkout is bootstrapped, not re-cloned"
  else
    fail "an unbootstrapped checkout is bootstrapped, not re-cloned (got '${flat:0:200}')"
  fi

  # A project that declares vcpkg packages gets vcpkg in the plan without
  # asking for it, prerequisites included.
  local declared="$WORK/81-setup/declared"
  mkdir -p "$declared/src"
  cat >"$declared/forge.lua" <<'LUA'
return {
  project = { name = "demo_declared", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {}, vcpkg = { fmt = "fmt::fmt" } },
  resources = { files = {} }, scripts = {}, features = {}
}
LUA
  printf 'int main() { return 0; }
' >"$declared/src/main.cpp"
  out="$(forge_in "$declared" setup --install --dry-run 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "bootstrap-vcpkg" <<<"$flat"; then
    pass "a project's own vcpkg dependency is planned without --tools"
  else
    fail "a project's own vcpkg dependency is planned without --tools (got '${flat:0:200}')"
  fi

  # vcpkg's own prerequisites are part of the plan when one is missing.
  if ! command -v zip >/dev/null 2>&1; then
    if grep -qF "needs:" <<<"$flat" && grep -qF "zip" <<<"$flat"; then
      pass "vcpkg's missing prerequisites are planned"
    else
      fail "vcpkg's missing prerequisites are planned (got '${flat:0:200}')"
    fi
  else
    skip "every vcpkg prerequisite is installed"
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
