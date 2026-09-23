# Toolchain selection (compilers, prefix path, cross-compilation, generator),
# the CMakePresets.json mirror, and multi-config generators.
scenario_64_toolchain_presets() {
  local base="$WORK/64-toolchain-presets"

  # --- toolchain settings ----------------------------------------------------
  local root="$base/toolchain"
  local bin="$root/bin"
  mkdir -p "$bin" "$root/src"
  make_plain_project "$root" demo_toolchain
  for tool in cmake ctest; do
    cat >"$bin/$tool" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$0") $*" >>"${TOOL_LOG:-/dev/null}"
exit 0
STUB
    chmod +x "$bin/$tool"
  done
  export TOOL_LOG="$root/tools.log"

  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_toolchain", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = {
    cxx_compiler = "/usr/bin/g++",
    c_compiler = "/usr/bin/gcc",
    cmake_prefix_path = { "/opt/sdk", "/opt/other" },
    system_name = "Linux",
    system_processor = "aarch64",
    generator = "Ninja",
    jobs = 2
  }
}
LUA
  printf 'int main() { return 0; }\n' >"$root/src/main.cpp"

  local old_path="$PATH"
  export PATH="$bin:$PATH"

  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/tools.log" "-DCMAKE_CXX_COMPILER=/usr/bin/g++" "cxx_compiler passed"
  assert_contains "$root/tools.log" "-DCMAKE_C_COMPILER=/usr/bin/gcc" "c_compiler passed"
  assert_contains "$root/tools.log" "-DCMAKE_PREFIX_PATH=/opt/sdk;/opt/other" "prefix path joined"
  assert_contains "$root/tools.log" "-DCMAKE_SYSTEM_NAME=Linux" "system name passed"
  assert_contains "$root/tools.log" "-DCMAKE_SYSTEM_PROCESSOR=aarch64" "system processor passed"
  assert_contains "$root/tools.log" "-G Ninja" "generator passed"
  assert_contains "$root/tools.log" "cmake --build build --parallel 2" "build.jobs is the default"

  : >"$root/tools.log"
  forge_in "$root" build --jobs 5 >/dev/null 2>&1 || true
  assert_contains "$root/tools.log" "cmake --build build --parallel 5" "--jobs overrides build.jobs"

  # --- CMakePresets.json -----------------------------------------------------
  assert_exists "$root/CMakePresets.json" "CMakePresets.json written"
  local json
  json="$(cat "$root/CMakePresets.json")"
  if python3 -c "import json,sys; json.loads(sys.argv[1])" "$json" >/dev/null 2>&1; then
    pass "CMakePresets.json parses"
  else
    fail "CMakePresets.json parses"
  fi
  if [[ "$json" == *'"name": "forge"'* && "$json" == *'"generator": "Ninja"'* &&
    "$json" == *'"CMAKE_CXX_COMPILER": "/usr/bin/g++"'* && "$json" == *'"binaryDir"'* ]]; then
    pass "CMakePresets.json mirrors the configure settings"
  else
    fail "CMakePresets.json mirrors the configure settings"
  fi

  # A hand-written preset file is not clobbered.
  printf '{\n  "version": 4,\n  "configurePresets": [{ "name": "mine" }]\n}\n' \
    >"$root/CMakePresets.json"
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/CMakePresets.json" '"name": "mine"' "a hand-written preset file is kept"

  export PATH="$old_path"

  # --- multi-config generator ------------------------------------------------
  local multi="$base/multi-config"
  local mbin="$multi/bin"
  mkdir -p "$mbin" "$multi/src"
  make_plain_project "$multi" demo_multi
  for tool in cmake ctest; do
    cat >"$mbin/$tool" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$0") $*" >>"${TOOL_LOG:-/dev/null}"
exit 0
STUB
    chmod +x "$mbin/$tool"
  done
  cat >"$multi/forge.lua" <<'LUA'
return {
  project = { name = "demo_multi", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { generator = "Ninja Multi-Config" }
}
LUA
  printf 'int main() { return 0; }\n' >"$multi/src/main.cpp"
  export TOOL_LOG="$multi/tools.log"
  export PATH="$mbin:$PATH"

  forge_in "$multi" build --release >/dev/null 2>&1 || true
  assert_lacks "$multi/tools.log" "CMAKE_BUILD_TYPE" "multi-config omits CMAKE_BUILD_TYPE"
  assert_contains "$multi/tools.log" "cmake --build build --parallel --config Release" \
    "multi-config builds with --config"
  export PATH="$old_path"

  # --- a generator with a space stays one argument ---------------------------
  local spaced="$base/spaced-generator"
  local sbin="$spaced/bin"
  mkdir -p "$sbin" "$spaced/src"
  make_plain_project "$spaced" demo_spaced
  cat >"$sbin/cmake" <<'STUB'
#!/usr/bin/env bash
for arg in "$@"; do echo "[$arg]" >>"${ARG_LOG:-/dev/null}"; done
exit 0
STUB
  chmod +x "$sbin/cmake"
  cat >"$spaced/forge.lua" <<'LUA'
return {
  project = { name = "demo_spaced", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { generator = "Unix Makefiles" }
}
LUA
  printf 'int main() { return 0; }
' >"$spaced/src/main.cpp"
  export ARG_LOG="$spaced/args.log"
  export PATH="$sbin:$PATH"
  forge_in "$spaced" build >/dev/null 2>&1 || true
  export PATH="$old_path"
  if grep -qF "[Unix Makefiles]" "$spaced/args.log"; then
    pass "a generator with a space stays one argument"
  else
    fail "a generator with a space stays one argument"
  fi

  # --- explicit toolchain_file ----------------------------------------------
  local tc="$base/toolchain-file"
  mkdir -p "$tc/src"
  make_plain_project "$tc" demo_toolchainfile
  printf 'int main() { return 0; }\n' >"$tc/src/main.cpp"
  cat >"$tc/forge.lua" <<'LUA'
return {
  project = { name = "demo_toolchainfile", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {}, vcpkg = { fmt = "fmt::fmt" } },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { toolchain_file = "/opt/my-toolchain.cmake" }
}
LUA
  assert_exit 1 "toolchain_file with vcpkg fails" forge_in "$tc" build
  local out flat
  out="$(forge_in "$tc" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "cannot be combined with Conan or vcpkg" <<<"$flat"; then
    pass "the toolchain conflict is explained"
  else
    fail "the toolchain conflict is explained"
  fi
}
