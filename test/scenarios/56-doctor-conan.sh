# `forge doctor`'s dependency sections: the conan.lock report and the
# transitive-dependency conflict check (stub conan supplies the graph).
scenario_56_doctor_conan() {
  local root="$WORK/56-doctor-conan"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src" "$root/external"
  make_plain_project "$root" demo_doctor_conan

  # Stub conan: `conan graph info ... --format=json` answers with a graph whose
  # transitive dependency collides with a git dependency of the project.
  cat >"$bin/conan" <<'STUB'
#!/usr/bin/env bash
if [[ "$1" == "graph" ]]; then
  cat <<'JSON'
{"graph":{"nodes":{"0":{"ref":"fmt/10.2.1"},"1":{"ref":"spdlog/1.12.0"}}}}
JSON
fi
exit 0
STUB
  chmod +x "$bin/conan"

  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_doctor_conan", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      spdlog = { git = "https://github.com/gabime/spdlog.git", tag = "v1.12.0" }
    },
    conan = { fmt = "10.2.1" }
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"

  local old_path="$PATH"
  export PATH="$bin:$PATH"

  # Without conan.lock the report says it is missing.
  local out flat
  out="$(forge_in "$root" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "conan.lock - not installed yet" <<<"$flat"; then
    pass "missing conan.lock reported"
  else
    fail "missing conan.lock reported"
  fi

  # With it, the report says installed.
  printf 'lock\n' >"$root/conan.lock"
  out="$(forge_in "$root" doctor 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "conan.lock - installed" <<<"$flat"; then
    pass "present conan.lock reported"
  else
    fail "present conan.lock reported"
  fi

  # The conflict check compares conan's transitive deps with the git ones.
  if grep -qF "'fmt' (Conan) pulls in 'spdlog' transitively" <<<"$flat"; then
    pass "transitive conan/git conflict reported"
  else
    fail "transitive conan/git conflict reported"
  fi

  export PATH="$old_path"
}
