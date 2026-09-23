# Per-dependency CMake options: emitted as cache variables before the fetch, so
# a dependency's own option() calls (tests, examples, backends) see them.
scenario_79_dependency_options() {
  local base="$WORK/79-dependency-options"
  local dep="$base/dep"
  local app="$base/app"

  mkdir -p "$dep/src"
  cat >"$dep/forge.lua" <<'LUA'
return {
  project = { name = "opt_lib", type = "library", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf 'int opt_value() { return 42; }\n' >"$dep/src/lib.cpp"

  mkdir -p "$app/src"
  cat >"$app/forge.lua" <<'LUA'
return {
  project = { name = "opt_app", type = "executable", standard = "20" },
  dependencies = {
    direct = {
      opt_lib = {
        path = "../dep",
        target = "opt_lib",
        options = { OPT_TESTS = "OFF", OPT_BACKEND = "metal" }
      }
    },
    conan = {}
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  printf '#include <cstdio>\nint opt_value();\nint main() { std::printf("%%d\\n", opt_value()); return 0; }\n' \
    >"$app/src/main.cpp"

  # The dependency must have been built once (Forge writes its CMake then).
  forge_in "$dep" build >/dev/null 2>&1 || true

  if ! forge_in "$app" build >/dev/null 2>&1; then
    fail "a consumer with dependency options builds"
    return
  fi
  pass "a consumer with dependency options builds"

  local cmake="$app/.config/cmake/CMakeLists.txt"
  assert_contains "$cmake" 'set(OPT_TESTS "OFF" CACHE STRING "" FORCE)' \
    "the option is emitted as a cache variable"
  assert_order "$(flatten <"$cmake")" "options are set before the dependency is configured" \
    'set(OPT_TESTS "OFF" CACHE STRING "" FORCE)' "FetchContent_MakeAvailable(opt_lib)"

  # The proof it reached CMake: the variable is in the cache of the consumer's
  # configure, which is what the dependency's option() reads.
  assert_contains "$app/build/CMakeCache.txt" "OPT_TESTS:STRING=OFF" \
    "the option reaches the CMake cache"
  assert_contains "$app/build/CMakeCache.txt" "OPT_BACKEND:STRING=metal" \
    "string options are passed through"
  assert_runs "$app/build/opt_app" "42" "the consumer still links the dependency"
}
