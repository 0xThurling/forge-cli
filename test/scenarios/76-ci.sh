# `forge ci`: generate a workflow tailored to the configuration, keep it
# reproducible (`--check`), and never clobber a hand-edited one.
scenario_76_ci() {
  local base="$WORK/76-ci"

  # --- a plain project ------------------------------------------------------
  local plain="$base/plain"
  mkdir -p "$plain/src"
  make_plain_project "$plain" demo_ci

  assert_exit 0 "forge ci writes a workflow" forge_in "$plain" ci
  local workflow="$plain/.github/workflows/forge.yml"
  assert_exists "$workflow" "the workflow lands in .github/workflows"
  assert_contains "$workflow" "runs-on: \${{ matrix.runner }}" "the job runs on the matrix runner"
  assert_contains "$workflow" "runner: [ubuntu-latest, macos-latest]" "the default runners are used"
  assert_contains "$workflow" "config: [release, debug]" "both configurations are built"
  assert_contains "$workflow" "forge build --\${{ matrix.config }}" "the build step follows the matrix"
  assert_lacks "$workflow" "forge test" "no test step without testing"
  assert_lacks "$workflow" "forge publish" "no package step without a version"

  # --- reproducibility ------------------------------------------------------
  assert_exit 0 "an untouched workflow is up to date" forge_in "$plain" ci --check
  printf '# hand edit\n' >>"$workflow"
  assert_exit 1 "a hand-edited workflow is reported as out of date" forge_in "$plain" ci --check
  assert_exit 1 "an existing workflow is not overwritten" forge_in "$plain" ci
  if grep -qF "# hand edit" "$workflow"; then
    pass "the hand edit survives"
  else
    fail "the hand edit survives"
  fi
  assert_exit 0 "forcing overwrites the workflow" forge_in "$plain" ci --force
  assert_exit 0 "the regenerated workflow is up to date" forge_in "$plain" ci --check

  # --- runners and configurations are configurable --------------------------
  assert_exit 0 "runners and configurations can be narrowed" \
    forge_in "$plain" ci --force --runners ubuntu-latest --configurations release
  assert_contains "$workflow" "runner: [ubuntu-latest]" "the requested runner is used"
  assert_contains "$workflow" "config: [release]" "the requested configuration is used"
  assert_lacks "$workflow" "macos-latest" "the other runner is gone"
  assert_lacks "$workflow" "debug" "the other configuration is gone"

  # --- a fully configured project -------------------------------------------
  local full="$base/full"
  mkdir -p "$full/src"
  cat >"$full/forge.lua" <<'LUA'
return {
  project = { name = "demo_ci_full", type = "library", standard = "20", version = "2.0.0" },
  dependencies = {
    direct = {},
    conan = { fmt = "10.2.1" },
    vcpkg = { spdlog = "spdlog::spdlog" },
    pkgconfig = { "zlib", "openssl" }
  },
  resources = { files = {} },
  scripts = {},
  features = {},
  testing = true
}
LUA
  printf 'int demo() { return 1; }\n' >"$full/src/lib.cpp"
  : >"$full/.clang-tidy"
  : >"$full/.clang-format"

  assert_exit 0 "forge ci writes a workflow for a configured project" forge_in "$full" ci --force
  workflow="$full/.github/workflows/forge.yml"
  assert_contains "$workflow" "forge test --\${{ matrix.config }}" "testing adds a test step"
  assert_contains "$workflow" "pipx install conan" "conan adds an install step"
  assert_contains "$workflow" "VCPKG_ROOT" "vcpkg sets VCPKG_ROOT"
  assert_contains "$workflow" "install the development packages" "pkg-config modules are flagged"
  assert_contains "$workflow" "zlib, openssl" "the pkg-config modules are listed"
  assert_contains "$workflow" "forge lint" "an opted-in .clang-tidy adds a lint step"
  assert_contains "$workflow" "forge format --check" "an opted-in .clang-format adds a format check"
  assert_contains "$workflow" "forge publish --format TGZ" "a versioned project packages"
  assert_contains "$workflow" "upload-artifact" "the package is uploaded"

  # --- the GitLab provider --------------------------------------------------
  assert_exit 0 "the gitlab provider writes a pipeline" \
    forge_in "$full" ci --force --provider gitlab
  local pipeline="$full/.gitlab-ci.yml"
  assert_exists "$pipeline" "the pipeline lands in .gitlab-ci.yml"
  assert_contains "$pipeline" "stages: [build]" "the pipeline has a build stage"
  assert_contains "$pipeline" "forge build --release" "the release job builds"
  assert_contains "$pipeline" "forge test --release" "the release job tests"
  assert_contains "$pipeline" "artifacts:" "the versioned job publishes artifacts"

  # --- an unknown provider --------------------------------------------------
  local out
  out="$(forge_in "$plain" ci --provider nope 2>&1 || true)"
  if grep -qF "unknown provider" <<<"$(flatten <<<"$out")"; then
    pass "an unknown provider is rejected"
  else
    fail "an unknown provider is rejected"
  fi
}
