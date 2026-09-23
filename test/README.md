# Forge end-to-end tests

Drives the CLI the way a user would — scaffolding projects, generating CMake,
and building with cmake/git/tar — against a throwaway workspace. No external
network, no sudo, and the installed `forge` is never touched: the suite runs the
dev build via `dotnet bin/Release/net10.0/forge.dll`. Downloads are exercised
against a `python3 -m http.server` on localhost (those scenarios skip without
python3); git dependencies use a local `file://` repository, and the Conan
scenario puts a stub `conan` on `PATH` that mimics CMakeDeps/CMakeToolchain, so
it runs the same way with or without a real Conan install.

## Running

```bash
test/run.sh                # build + every scenario
test/run.sh path cache     # only scenarios whose id contains a filter
test/run.sh --list         # list the scenarios
test/run.sh --keep         # keep the workspace (auto-kept on failure)
test/run.sh --no-build     # use the existing build
test/run.sh --verbose      # show the build summary
```

The exit code is non-zero if any assertion fails. A failing run prints the
scratch workspace path so the generated projects can be inspected.

## Scenarios

**Commands**

| Id | Covers |
|---|---|
| `10-build` | minimal build, generated CMake, compile DB, verbose mode, rebuild |
| `20-scaffold` | `forge create` (executable + `--type library`), `forge new class/header/source/struct` |
| `21-doctor` | `forge doctor` on a healthy project |
| `22-project-commands` | `project info/tree/stats/dependencies/scripts` |
| `23-scripts` | script listing, execution, exit-code propagation |
| `24-resources` | `forge embed` + generated embedded resources (no duplicates) |
| `25-clean` | `forge clean` removes `build/` and is idempotent |
| `32-download` | `download`/`extract`/`fetch` against a local server: hashes, 404s, zip/tar/tar.gz, strip |
| `35-test-filter` | `forge test --filter` on `../fp` *(skips without it)* |
| `37-config-migrate` | legacy `package.toml` → `forge.lua` |
| `40-doctor-layout` | doctor on a broken layout: missing/optional directories |
| `41-dependencies` | dependency listing across git, `path` and Conan |
| `44-run-command` | `forge run` (no args, library, unknown script) |
| `46-target-defaults` | dependency without `target`, source-less library, two channels |
| `47-build-hooks` | pre/post-build scripts, and a failing pre-build |
| `48-cli-surface` | `--version`, `--help`, no args, unknown command, bare parents |
| `49-subdir-build` | building from a subdirectory; nested build dirs excluded |
| `51-new-contents` | `forge new class/struct/header/source` file contents |
| `52-build-flags-aliases` | `cxx_flags`/`link_flags`/`compile_definitions` and their aliases |
| `53-lua-environment` | `forge.os` / `forge.distro` / `forge.package_manager` / `current_working_dir` |
| `54-generated-headers` | headers generated from `.cpp`-only libraries; hand-written ones win |
| `55-testing-scaffold` | `testing = true` scaffolding + the test section + the CMake invocation |
| `56-doctor-conan` | doctor's `conan.lock` report and transitive conan/git conflict check |
| `57-test-flags` | `forge test` flag pass-through and the ctest filter |

**Dependencies and the generated CMake**

| Id | Covers |
|---|---|
| `11-path-dep` | local `path`: `SOURCE_DIR`, nothing fetched, links and runs |
| `12-missing-path` | a missing path is reported with its resolved location |
| `13-git-dep` | git dependency fetches (local `file://`) and links |
| `17-build-flags` | `--standard`, `--release`/`--debug`, `--preset`, `--no-config-presets` |
| `18-features` | `features` table/scalar forms, options, `has_feature`/`get_feature_option` |
| `19-target-linkage` | static/shared libraries, `install_headers`, `cmake_policy_version` |
| `28-presets` | flag presets + raw flags |
| `36-conan` | the whole Conan path via a stub `conan`: conanfile contents, the exact `conan install` arguments, parsing `find_package`/`target_link_libraries` into the generated CMake, the conan toolchain being read by CMake, a failing conan, `install --prefix` |

**Lua and build scripts**

| Id | Covers |
|---|---|
| `16-lua-script` | `cmakeOptions` keys + pre/post `add_cmake` phases |
| `29-lua-api` | `log.*`, `config.get/set`, `pull_repo`, `get_packages`, unknown-key warning |
| `33-lua-files` | `forge.download` (with options), `forge.fetch`, `forge.extract` |
| `34-lua-errors` | runtime and syntax errors in a build script fail cleanly |
| `45-resources-data` | embedded bytes round-trip; same-named resources stay distinct |
| `50-get-packages` | `forge.get_packages` (nopass and sudo-with-password paths) |
| `42-lua-scripts` | several scripts and their merge order, value shapes/escaping, `${PROJECT_NAME}`, `config.set` persistence |
| `43-forge-lua-errors` | `forge.lua` itself: syntax error, missing name, non-table return, unknown keys |

**Failure paths**

| Id | Covers |
|---|---|
| `14-stale-cache` | `build/` from another source tree is regenerated |
| `15-mirror-headers` | header install: siblings verbatim, others rewritten |
| `26-errors` | not-a-project, unknown preset |
| `27-install` | `forge install` is a no-op without Conan |
| `38-build-failure` | compile error fails the build; fixing it passes |
| `39-embed-errors` | embed failure paths |
| `30-fp-integration` | `../fp`: mirror byte-identical, git status unchanged *(skips)* |
| `31-fp-test` | `forge test` on `../fp` *(skips)* |

**Toolchain, packaging and workspaces**

| Id | Covers |
|---|---|
| `58-lockfile` | `forge.lock`: git refs pinned to commits, builds use the lock, `--update` |
| `59-add-remove` | `forge add`/`remove` for git, path and conan dependencies |
| `60-parallel-and-launcher` | parallel builds by default, `--jobs`, ccache/sccache auto-detection |
| `61-json-output` | `--json` for the data commands (info, dependencies) |
| `62-vcpkg` | the vcpkg channel: manifest generation, toolchain hand-off, baseline |
| `63-library-export` | version/SOVERSION, `install(EXPORT)`, a real `find_package` consumer |
| `64-toolchain-presets` | compiler/prefix/cross/generator selection, `--config`, `CMakePresets.json` |
| `65-pkgconfig` | the pkg-config channel and `pkg_check_modules` wiring |
| `66-project-hooks` | the `templates/` and `commands/` project hooks |
| `67-format-lint` | `forge format` (check/rewrite, generated config) and `forge lint` (real clang-tidy findings) |
| `68-test-frameworks` | catch2/doctest wiring, the benchmark target, `forge bench` |
| `69-doctor-fix-completions` | `doctor --fix` repairs a layout; completion scripts for bash/zsh/fish |
| `70-build-toggles` | unity builds, precompiled headers, module scanning (incl. the Ninja switch) |
| `71-outdated-vendor` | `forge outdated` tag comparison; `forge vendor` + a build with the repo deleted |
| `72-workspaces` | discovery, dependency ordering, `workspace list/build/test`, explicit `forge.workspace.lua` |
| `73-lua-sections` | `forge.add_section`: anchors, ordering, replacement, validation |
| `74-publish` | CPack packaging for libraries and executables (TGZ/ZIP/DEB) and its failure paths |
| `75-build-context` | Lua contributions belong to one build: no duplication across builds, identical regeneration |
| `76-ci` | `forge ci`: generated workflow content, reproducibility (`--check`), `--force`, GitLab provider |
| `77-dependency-isolation` | a dependency's tests, test framework fetch and CPack config stop at the dependency |
| `78-docs-coverage` | docs stay in step: every command/subcommand is in the CLI reference, the nav matches the pages, recent features are described |
| `79-dependency-options` | per-dependency CMake options reach the cache the dependency's `option()` reads |
| `80-lua-workflow` | `forge.exec` output capture, file/template helpers, `forge.git.*`, `cacheVariables`, refreshed editor stubs |
| `81-setup` | `forge setup` report, `--json`, unknown-tool rejection, install plan, `doctor`'s toolchain section |
| `82-version-from-git` | `version_from_git`: tag → version, commits → fourth component, no tag → declared version |
| `83-upgrade` | `forge upgrade`: report, `--json`, unknown dependency, `--apply` rewrites the tag and re-pins the lock |
| `84-watch` | `forge watch`: build once and exit, rebuild on a real file change, command/watch-list validation |
| `85-init` | `forge init`: adopts a directory with existing sources, layout/ignore/stubs, refuses a Forge project, input validation |
| `86-why` | `forge why`: every direct channel, a transitive conan package via the graph, `--json`, unknown names |
| `87-dependency-cache` | shared cache: reused checkout, `FETCHCONTENT_SOURCE_DIR`, branches not cached, offline build, `cache list/clear` |
| `88-multi-target` | extra executables/libraries: sources, link lines, install rules, `run --bin`, config round-trip, validation |
| `89-markup-balance` | every Spectre markup string in the CLI is balanced (an unclosed tag throws while the console refreshes) |
| `90-json-and-channels` | `forge test` as a CI gate (exit code + `--json` summary), and `forge add --vcpkg` / `--pkg-config` |

## Adding a scenario

Drop `test/scenarios/<id>.sh` defining `scenario_<id_with_underscores>()`:

```bash
scenario_40_my_feature() {
  local root="$WORK/40-my-feature"
  make_plain_project "$root" demo          # helpers live in test/lib.sh
  assert_exit 0 "builds" forge_in "$root" build
}
```

Conventions:

- keep scenario variables `local` — scenarios share the runner's shell so the
  pass/fail counters work;
- use `$WORK` for scratch files and `$REPO` for the checkout;
- prefer the `assert_*` helpers (they record the result and print diagnostics);
- call `skip "<why>"` when a prerequisite is missing instead of failing.
