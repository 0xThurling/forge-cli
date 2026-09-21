# Library header install: sibling includes verbatim, others rewritten.
scenario_15_mirror_headers() {
  local root="$WORK/15-mirror-headers"
  mkdir -p "$root/src/fp" "$root/src/other"
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_hdrs", type = "library", standard = "20", install_headers = true },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#pragma once\ninline int b() { return 1; }\n' >"$root/src/fp/b.hpp"
  printf '#pragma once\n#include "b.hpp"\ninline int sibling() { return b(); }\n' \
    >"$root/src/fp/sibling.hpp"
  printf '#pragma once\n#include "fp/b.hpp"\ninline int pathstyle() { return b(); }\n' \
    >"$root/src/fp/pathstyle.hpp"
  printf '#pragma once\n#include "b.hpp"\ninline int cross() { return b(); }\n' \
    >"$root/src/other/cross.hpp"
  printf 'namespace demo { int answer() { return 42; } }\n' >"$root/src/demo_hdrs.cpp"

  forge_in "$root" build >/dev/null 2>&1 || true

  assert_contains "$root/include/demo_hdrs/fp/sibling.hpp" '#include "b.hpp"' \
    "sibling include copied verbatim"
  assert_lacks "$root/include/demo_hdrs/fp/sibling.hpp" 'demo_hdrs/fp/b.hpp' \
    "sibling include not prefixed"
  assert_contains "$root/include/demo_hdrs/fp/pathstyle.hpp" \
    '#include "demo_hdrs/fp/b.hpp"' "path-style include rewritten"
  assert_contains "$root/include/demo_hdrs/other/cross.hpp" \
    '#include "demo_hdrs/fp/b.hpp"' "cross-directory include rewritten"
}
