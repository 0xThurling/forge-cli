# `forge why`: direct dependencies are read from forge.lua; transitive ones are
# explained through Conan's resolved graph.
scenario_86_why() {
  local root="$WORK/86-why"
  mkdir -p "$root/src" "$root/.config" "$root/bin"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_why", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      thelib = { git = "https://example.invalid/thelib.git", tag = "v1.0.0", target = "thelib" }
    },
    conan = { spdlog = "1.12.0" },
    vcpkg = { sdl = "SDL2::SDL2" },
    pkgconfig = { "zlib" }
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"
  printf '[]\n' >"$root/.config/conanfile.txt"

  # A stub conan that answers `graph info … --format=json` with the graph the
  # real one would produce: spdlog is direct, fmt comes in through it.
  cat >"$root/bin/conan" <<'STUB'
#!/usr/bin/env bash
cat <<'JSON'
{"graph": {"nodes": {
  "0": {"ref": null, "requires": ["1"]},
  "1": {"ref": "spdlog/1.12.0#abc", "requires": ["2"], "dependencies": {"0": {}}},
  "2": {"ref": "fmt/10.2.1#def", "dependencies": {"1": {"ref": "spdlog/1.12.0#abc"}}}
}}}
JSON
STUB
  chmod +x "$root/bin/conan"
  local old_path="$PATH"
  export PATH="$root/bin:$PATH"

  local out flat

  # --- a direct dependency of each channel ---------------------------------
  forge_in "$root" why thelib >/dev/null 2>&1 || true
  out="$(forge_in "$root" why thelib 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "direct dependency" <<<"$flat" && grep -qF "thelib.git" <<<"$flat"; then
    pass "a git dependency is reported as direct"
  else
    fail "a git dependency is reported as direct (got '${flat:0:160}')"
  fi

  out="$(forge_in "$root" why spdlog 2>&1 || true)"
  if grep -qF "direct dependency" <<<"$(flatten <<<"$out")"; then
    pass "a conan dependency is reported as direct"
  else
    fail "a conan dependency is reported as direct"
  fi

  out="$(forge_in "$root" why zlib 2>&1 || true)"
  if grep -qF "pkgconfig" <<<"$(flatten <<<"$out")"; then
    pass "a pkg-config module is reported"
  else
    fail "a pkg-config module is reported"
  fi

  # --- a transitive package ------------------------------------------------
  if out="$(forge_in "$root" why fmt 2>&1)"; then
    pass "a transitive package is found"
  else
    fail "a transitive package is found"
  fi
  flat="$(flatten <<<"$out")"
  if grep -qF "transitive" <<<"$flat" && grep -qF "required by" <<<"$flat" &&
    grep -qF "spdlog" <<<"$flat"; then
    pass "the requiring package is named"
  else
    fail "the requiring package is named (got '${flat:0:200}')"
  fi

  out="$(forge_in "$root" why fmt --json 2>&1 || true)"
  if [[ "$out" == *'"name":"fmt"'* && "$out" == *'"found":true'* && "$out" == *"spdlog/1.12.0"* ]]; then
    pass "the JSON report includes the chain"
  else
    fail "the JSON report includes the chain (got '${out:0:200}')"
  fi

  # --- unknown --------------------------------------------------------------
  assert_exit 1 "an unknown package fails" forge_in "$root" why nope
  out="$(forge_in "$root" why nope 2>&1 || true)"
  if grep -qF "not declared in forge.lua" <<<"$(flatten <<<"$out")"; then
    pass "an unknown package is explained"
  else
    fail "an unknown package is explained"
  fi

  export PATH="$old_path"
}
