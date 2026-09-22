# `forge format` and `forge lint`: generated tool configs, formatting checks,
# and clang-tidy gating on warnings.
scenario_67_format_lint() {
  if ! command -v clang-format >/dev/null 2>&1; then
    skip "clang-format is not installed"
    return
  fi

  local root="$WORK/67-format-lint"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_lint
  # Deliberately badly formatted, but semantically clean.
  printf '#include <cstdio>\nint main()\n{\n\tstd::printf( "hello from demo_lint\\n" );\n\treturn 0;\n}\n' \
    >"$root/src/main.cpp"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "the unformatted project builds"
    return
  fi
  pass "the unformatted project builds"

  # --- format ---------------------------------------------------------------
  assert_exit 1 "format --check fails on unformatted code" forge_in "$root" format --check
  assert_exit 0 "format rewrites the sources" forge_in "$root" format
  assert_exists "$root/.clang-format" "a .clang-format is written"
  assert_exit 0 "format --check passes afterwards" forge_in "$root" format --check
  # Portable tab check: -P is GNU-only.
  if grep -qF "$(printf '\t')" "$root/src/main.cpp"; then
    fail "tabs were replaced by spaces"
  else
    pass "tabs were replaced by spaces"
  fi

  # --- lint -----------------------------------------------------------------
  if ! command -v clang-tidy >/dev/null 2>&1; then
    skip "clang-tidy is not installed"
    return
  fi

  assert_exit 0 "lint passes on clean code" forge_in "$root" lint
  assert_exists "$root/.clang-tidy" "a .clang-tidy is written"

  # An assignment in a condition is a bugprone finding.
  printf '#include <cstdio>\nint main() {\n  int x = 0;\n  if (x = 1) {\n    std::printf("%%d", x);\n  }\n  return 0;\n}\n' \
    >"$root/src/main.cpp"
  local out
  if out="$(forge_in "$root" lint 2>&1)"; then
    fail "lint fails on a real warning"
  else
    pass "lint fails on a real warning"
  fi
  if grep -qiE "warning:|bugprone|clang-analyzer" <<<"$(flatten <<<"$out")"; then
    pass "the warning is reported"
  else
    fail "the warning is reported"
  fi

  # --allow-warnings reports without failing.
  assert_exit 0 "lint --allow-warnings exits 0" forge_in "$root" lint --allow-warnings

  # --- a noisy dependency does not fail the consumer ------------------------
  # clang-tidy walks into every header the sources include; without scoping,
  # a consumer of a header library drowns in warnings it cannot fix.
  local base="$WORK/67-format-lint"
  local noisy="$base/noisy"
  local consumer="$base/consumer"
  mkdir -p "$noisy/include" "$noisy/src" "$consumer/src"

  cat >"$noisy/forge.lua" <<'LUA'
return {
  project = { name = "noisy_lib", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  # performance-unnecessary-value-param on any caller.
  printf '#pragma once\n#include <string>\ninline int noisy_size(std::string text) { return (int)text.size(); }\n' \
    >"$noisy/include/noisy.hpp"
  printf '#include <string>\nint noisy_size(std::string);\nint unused_helper() { return 0; }\n' \
    >"$noisy/src/noisy.cpp"

  cat >"$consumer/forge.lua" <<'LUA'
return {
  project = { name = "noisy_consumer", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      noisy_lib = { path = "../noisy", target = "noisy_lib" }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\n#include "noisy.hpp"\nint main() { std::printf("%%d\\n", noisy_size("abc")); return 0; }\n' \
    >"$consumer/src/main.cpp"

  # An unbuilt path dependency is reported: CMake would silently skip it.
  local out flat
  out="$(forge_in "$consumer" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "has no CMakeLists.txt yet" <<<"$flat"; then
    pass "an unbuilt path dependency is reported"
  else
    fail "an unbuilt path dependency is reported"
  fi

  # Build the dependency's generated CMake (a stub keeps this offline), then
  # the consumer for real.
  local bin="$base/bin"
  mkdir -p "$bin"
  printf '#!/usr/bin/env bash\nexit 0\n' >"$bin/cmake"
  chmod +x "$bin/cmake"
  local old_path="$PATH"
  export PATH="$bin:$PATH"
  forge_in "$noisy" build >/dev/null 2>&1 || true
  export PATH="$old_path"

  if ! forge_in "$consumer" build >/dev/null 2>&1; then
    fail "the consumer of a noisy library builds"
  else
    pass "the consumer of a noisy library builds"
  fi

  if forge_in "$consumer" lint >/dev/null 2>&1; then
    pass "a dependency's headers do not fail the lint"
  else
    fail "a dependency's headers do not fail the lint"
  fi

  # The project's own findings still fail it (lint runs against the compile
  # database the build above produced).
  # (lint runs against the compile database the build above produced)
  printf '#include <cstdio>\n#include "noisy.hpp"\nint main() { int x = 0; if (x = 1) { } std::printf("%%d\\n", noisy_size("abc")); return 0; }\n' \
    >"$consumer/src/main.cpp"
  assert_exit 1 "the project's own findings still fail the lint" forge_in "$consumer" lint

  # A project that opts into header diagnostics gets them.
  printf 'HeaderFilterRegex: ".*"\n' >"$consumer/.clang-tidy"
  assert_exit 1 "an explicit HeaderFilterRegex takes over" forge_in "$consumer" lint
}
