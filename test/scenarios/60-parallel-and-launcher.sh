# Build speedups: parallel builds by default (with --jobs), and the
# ccache/sccache compiler launcher.
scenario_60_parallel_and_launcher() {
  local base="$WORK/60-parallel-and-launcher"

  # --- parallel builds ------------------------------------------------------
  local jobs="$base/jobs"
  local bin="$jobs/bin"
  mkdir -p "$bin" "$jobs/src"
  make_plain_project "$jobs" demo_jobs
  for tool in cmake ctest; do
    cat >"$bin/$tool" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$0") $*" >>"${TOOL_LOG:-/dev/null}"
exit 0
STUB
    chmod +x "$bin/$tool"
  done
  export TOOL_LOG="$jobs/tools.log"
  local old_path="$PATH"
  export PATH="$bin:$PATH"

  forge_in "$jobs" build >/dev/null 2>&1 || true
  assert_contains "$jobs/tools.log" "cmake --build build --parallel" "builds are parallel by default"

  : >"$jobs/tools.log"
  forge_in "$jobs" build --jobs 3 >/dev/null 2>&1 || true
  assert_contains "$jobs/tools.log" "cmake --build build --parallel 3" "--jobs sets the job count"

  : >"$jobs/tools.log"
  forge_in "$jobs" test --jobs 2 >/dev/null 2>&1 || true
  assert_contains "$jobs/tools.log" "cmake --build build --parallel 2" "--jobs is forwarded by forge test"

  export PATH="$old_path"

  # --- compiler launcher ----------------------------------------------------
  local launcher="$base/launcher"
  local lbin="$launcher/bin"
  mkdir -p "$lbin" "$launcher/src"
  make_plain_project "$launcher" demo_launcher

  # A ccache stand-in that records its use and runs the real compiler.
  cat >"$lbin/ccache" <<'STUB'
#!/usr/bin/env bash
echo "ccache $*" >>"${CCACHE_LOG:-/dev/null}"
exec "$@"
STUB
  chmod +x "$lbin/ccache"

  export PATH="$lbin:$PATH"
  export CCACHE_LOG="$launcher/ccache.log"

  forge_in "$launcher" build >/dev/null 2>&1 || true
  assert_contains "$launcher/.config/cmake/CMakeLists.txt" \
    "set(CMAKE_CXX_COMPILER_LAUNCHER ccache)" "ccache is detected and configured"
  assert_contains "$launcher/.config/cmake/CMakeLists.txt" \
    "set(CMAKE_C_COMPILER_LAUNCHER ccache)" "the C launcher is set too"
  if [[ -s "$launcher/ccache.log" ]]; then
    pass "the launcher is actually used by the build"
  else
    fail "the launcher is actually used by the build"
  fi
  assert_runs "$launcher/build/demo_launcher" "hello from demo_launcher" \
    "the build succeeds through the launcher"

  # Opt out explicitly.
  cat >"$launcher/forge.lua" <<'LUA'
return {
  project = { name = "demo_launcher", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { compiler_launcher = "none" }
}
LUA
  forge_in "$launcher" build >/dev/null 2>&1 || true
  assert_lacks "$launcher/.config/cmake/CMakeLists.txt" "COMPILER_LAUNCHER" \
    "compiler_launcher = none disables it"

  # An explicit launcher wins over auto-detection.
  cat >"$lbin/sccache" <<'STUB'
#!/usr/bin/env bash
exec "$@"
STUB
  chmod +x "$lbin/sccache"
  cat >"$launcher/forge.lua" <<'LUA'
return {
  project = { name = "demo_launcher", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { compiler_launcher = "sccache" }
}
LUA
  forge_in "$launcher" build >/dev/null 2>&1 || true
  assert_contains "$launcher/.config/cmake/CMakeLists.txt" \
    "set(CMAKE_CXX_COMPILER_LAUNCHER sccache)" "an explicit launcher is honoured"

  export PATH="$old_path"
}
