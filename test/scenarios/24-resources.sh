# `forge embed` registers a resource, and the build embeds its bytes.
scenario_24_resources() {
  local root="$WORK/24-resources"
  make_plain_project "$root" demo_res
  mkdir -p "$root/assets"
  printf 'asset payload\n' >"$root/assets/data.txt"

  if forge_in "$root" embed assets/data.txt >/dev/null 2>&1; then
    pass "forge embed registers a resource"
  else
    fail "forge embed registers a resource"
    return
  fi

  assert_contains "$root/forge.lua" 'assets/data.txt' "resource recorded in forge.lua"

  if ! forge_in "$root" build >/dev/null 2>&1; then
    fail "builds with a resource"
    return
  fi
  pass "builds with a resource"

  assert_exists "$root/src/embedded_resources.h" "generated resource header"
  assert_exists "$root/src/embedded_resources.cpp" "generated resource source"
  assert_contains "$root/src/embedded_resources.cpp" '// Resource: assets/data.txt' \
    "resource recorded in the generated source"

  # The generated file embeds the resource's bytes, so regenerating it on every
  # build would recompile it: an unchanged build must leave it alone.
  local resources_out
  resources_out="$(forge_in "$root" build 2>&1 || true)"
  if grep -qF "Generating resource files" <<<"$(flatten <<<"$resources_out")"; then
    fail "an unchanged build does not regenerate the resource source"
  else
    pass "an unchanged build does not regenerate the resource source"
  fi

  # Embedding the same file twice must not duplicate the entry.
  forge_in "$root" embed assets/data.txt >/dev/null 2>&1 || true
  local count
  count="$(grep -c 'assets/data.txt' "$root/forge.lua" || true)"
  if [[ "$count" -le 1 ]]; then
    pass "re-embedding does not duplicate the entry"
  else
    fail "re-embedding does not duplicate the entry (found $count)"
  fi
}
