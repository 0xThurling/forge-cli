# C++20 modules are compiled (they need a CXX_MODULES file set, not plain
# sources), and `forge bench` can save a baseline and compare against it.
scenario_91_modules_and_bench() {
  local base="$WORK/91-modules-and-bench"

  # --- modules --------------------------------------------------------------
  local mod="$base/mod"
  mkdir -p "$mod/src"
  make_plain_project "$mod" demo_mod
  cat >"$mod/forge.lua" <<'LUA'
return {
  project = { name = "demo_mod", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  build = { modules = true }
}
LUA
  cat >"$mod/src/mymod.cppm" <<'CPP'
export module mymod;

export int answer() { return 42; }
CPP
  printf 'import mymod;\n#include <cstdio>\nint main() { std::printf("%%d\\n", answer()); return 0; }\n' \
    >"$mod/src/main.cpp"

  forge_in "$mod" build >/dev/null 2>&1 || true
  local cmake="$mod/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "FILE_SET CXX_MODULES" "module interfaces are put in a CXX_MODULES file set"
  assert_contains "$cmake" "*.cppm" "module interfaces are globbed"

  if command -v ninja >/dev/null 2>&1; then
    if forge_in "$mod" build >/dev/null 2>&1; then
      assert_runs "$mod/build/demo_mod" "42" "a module interface compiles and is imported"
    else
      skip "this compiler cannot build modules (CMake needs Ninja + a C++20 module compiler)"
    fi
  else
    skip "ninja is not installed (module scanning needs it)"
  fi

  # --- bench: save and compare ---------------------------------------------
  local bench="$base/bench"
  mkdir -p "$bench/src" "$bench/build"
  cat >"$bench/forge.lua" <<'LUA'
return {
  project = { name = "demo_bench", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = { benchmark = true }
}
LUA
  printf 'int main() { return 0; }\n' >"$bench/src/main.cpp"
  # A stand-in for a Google Benchmark binary: it writes a report when asked.
  cat >"$bench/build/demo_bench_bench" <<'STUB'
#!/usr/bin/env bash
out=""
for arg in "$@"; do
  case "$arg" in --benchmark_out=*) out="${arg#--benchmark_out=}" ;; esac
done
value="${BENCH_NS:-100}"
[ -n "$out" ] && printf '{"context":{},"benchmarks":[{"name":"BM_Work","run_type":"iteration","real_time":%s,"cpu_time":%s,"time_unit":"ns","iterations":1000}]}\n' "$value" "$value" >"$out"
exit 0
STUB
  chmod +x "$bench/build/demo_bench_bench"

  local out
  export BENCH_NS=100
  out="$(forge_in "$bench" bench --no-build --save baseline.json 2>&1 || true)"
  if grep -qF "Saved" <<<"$(flatten <<<"$out")"; then
    pass "bench --save writes a baseline"
  else
    fail "bench --save writes a baseline (got '$(flatten <<<"$out" | head -c 120)')"
  fi

  export BENCH_NS=150
  out="$(forge_in "$bench" bench --no-build --compare baseline.json 2>&1 || true)"
  if grep -qF "+50.0%" <<<"$(flatten <<<"$out")"; then
    pass "bench --compare reports the regression"
  else
    fail "bench --compare reports the regression (got '$(flatten <<<"$out" | head -c 160)')"
  fi

  export BENCH_NS=90
  out="$(forge_in "$bench" bench --no-build --compare baseline.json 2>&1 || true)"
  # `--` so a pattern starting with a dash is not read as an option.
  if grep -qF -- "-10.0%" <<<"$(flatten <<<"$out")"; then
    pass "bench --compare reports an improvement"
  else
    fail "bench --compare reports an improvement (got '$(flatten <<<"$out" | head -c 160)')"
  fi

  # The gate: a regression beyond the threshold fails the command.
  export BENCH_NS=150
  if forge_in "$bench" bench --no-build --compare baseline.json --fail-over 10 >/dev/null 2>&1; then
    fail "bench --fail-over fails a regression"
  else
    pass "bench --fail-over fails a regression"
  fi
  if forge_in "$bench" bench --no-build --compare baseline.json --fail-over 100 >/dev/null 2>&1; then
    pass "a regression inside the threshold passes"
  else
    fail "a regression inside the threshold passes"
  fi

  out="$(forge_in "$bench" bench --no-build --compare baseline.json --json 2>&1 || true)"
  if [[ "$out" == *'"changePercent":50.00'* ]]; then
    pass "bench --compare --json reports the delta"
  else
    fail "bench --compare --json reports the delta (got '${out:0:160}')"
  fi
}
