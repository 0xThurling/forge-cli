# Embedded resources: the bytes must round-trip through the generated header,
# and two files sharing a basename must stay distinct.
scenario_45_resources_data() {
  local root="$WORK/45-resources-data"
  mkdir -p "$root/src" "$root/assets/nested"
  make_plain_project "$root" demo_res
  printf 'first-resource\n' >"$root/assets/data.txt"
  printf 'second-resource\n' >"$root/assets/nested/data.txt"

  forge_in "$root" embed assets/data.txt >/dev/null 2>&1 || true
  forge_in "$root" embed assets/nested/data.txt >/dev/null 2>&1 || true

  cat >"$root/src/main.cpp" <<'CPP'
#include "embedded_resources.h"

#include <cstdio>
#include <string>

int main() {
  const auto &a = Embedded::get("assets/data.txt");
  const auto &b = Embedded::get("assets/nested/data.txt");
  std::printf("a=%s", std::string(reinterpret_cast<const char *>(a.data), a.size).c_str());
  std::printf("b=%s", std::string(reinterpret_cast<const char *>(b.data), b.size).c_str());
  return 0;
}
CPP

  local out
  if ! out="$(forge_in "$root" build 2>&1)"; then
    fail "builds with two embedded resources"
    tail -5 <<<"$out"
    return
  fi
  pass "builds with two embedded resources"

  local generated="$root/src/embedded_resources.cpp"
  assert_contains "$generated" '"assets/data.txt"' "resource keyed by its registered path"
  assert_contains "$generated" '"assets/nested/data.txt"' "nested resource keyed separately"

  if out="$("$root/build/demo_res" 2>&1)"; then
    pass "the resource reader runs"
  else
    fail "the resource reader runs"
  fi
  if [[ "$out" == *"a=first-resource"* && "$out" == *"b=second-resource"* ]]; then
    pass "both resources round-trip with their own bytes"
  else
    fail "both resources round-trip with their own bytes (got '$(printf '%.80s' "$out")')"
  fi
}
