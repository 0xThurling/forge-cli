# `features`: table form with options, scalar shortcut, disabled entries — and
# the Lua view of the same data.
scenario_18_features() {
  local root="$WORK/18-features"
  make_plain_project "$root" demo_features
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_features", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {
    graphics = { enabled = true, backend = "vulkan" },
    network = true,
    disabled = false
  }
}
LUA
  mkdir -p "$root/.config/forge/build"
  cat >"$root/.config/forge/build/features.lua" <<'LUA'
forge.log.info("graphics=" .. tostring(forge.config.has_feature("graphics")))
forge.log.info("network=" .. tostring(forge.config.has_feature("network")))
forge.log.info("disabled=" .. tostring(forge.config.has_feature("disabled")))
forge.log.info("backend=" .. tostring(forge.config.get_feature_option("graphics", "backend", "MISSING")))
return {}
LUA

  local out flat
  out="$(forge_in "$root" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"

  local cmake="$root/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" "add_compile_definitions(FEATURE_GRAPHICS)" \
    "table feature emits its define"
  assert_contains "$cmake" 'add_compile_definitions(FEATURE_GRAPHICS_BACKEND="vulkan")' \
    "feature option emits a define"
  assert_contains "$cmake" "add_compile_definitions(FEATURE_NETWORK)" \
    "scalar feature emits its define"
  assert_lacks "$cmake" "FEATURE_DISABLED" "disabled feature is omitted"

  if grep -qF "graphics=true" <<<"$flat"; then
    pass "has_feature(table form)"
  else
    fail "has_feature(table form)"
  fi
  if grep -qF "network=true" <<<"$flat"; then
    pass "has_feature(scalar form)"
  else
    fail "has_feature(scalar form)"
  fi
  if grep -qF "disabled=false" <<<"$flat"; then
    pass "has_feature(false)"
  else
    fail "has_feature(false)"
  fi
  if grep -qF "backend=vulkan" <<<"$flat"; then
    pass "get_feature_option"
  else
    fail "get_feature_option"
  fi
}
