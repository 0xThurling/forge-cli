# A library with only .cpp files gets headers generated from them.
scenario_54_generated_headers() {
  local root="$WORK/54-generated-headers"
  mkdir -p "$root/src"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_gen", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  cat >"$root/src/math_utils.cpp" <<'CPP'
#include <cmath>
#include <vector>

namespace demo {

int add(int a, int b) { return a + b; }

double mean(const std::vector<double>& xs) {
  double total = 0;
  for (double x : xs) total += x;
  return total / static_cast<double>(xs.size());
}

}  // namespace demo
CPP

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "a .cpp-only library builds"
    return
  fi
  pass "a .cpp-only library builds"

  local header="$root/include/demo_gen/math_utils.hpp"
  assert_exists "$header" "a header is generated for the .cpp"
  assert_contains "$header" "// Auto-generated from src/math_utils.cpp" "generated header is marked"
  assert_contains "$header" "#include <vector>" "includes carried over"
  assert_contains "$header" "namespace demo {" "namespace carried over"
  assert_contains "$header" "int add(int a, int b);" "function declaration extracted"
  assert_contains "$header" "double mean(const std::vector<double>& xs);" \
    "declaration with parameters extracted"

  # A hand-written header wins over the generated one.
  printf '#pragma once\n// hand written\nnamespace demo { int add(int, int); }\n' \
    >"$root/src/math_utils.hpp"
  forge_in "$root" build >/dev/null 2>&1 || true
  assert_contains "$root/include/demo_gen/math_utils.hpp" "// hand written" \
    "a hand-written header is copied instead of generated"
}
