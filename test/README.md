# Forge end-to-end tests

Drives the CLI the way a user would — scaffolding projects, generating CMake,
and building with cmake/git/tar — against a throwaway workspace. No network, no
sudo, and the installed `forge` is never touched: the suite runs the dev build
via `dotnet bin/Release/net10.0/forge.dll`.

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

| Id | Covers |
|---|---|
| `10-build` | minimal project builds, CMake + compile DB generated, binary runs |
| `11-path-dep` | local `path` dependency: `SOURCE_DIR`, nothing fetched, links and runs |
| `12-missing-path` | a missing path is reported with its resolved location |
| `13-git-dep` | git dependency fetches (local `file://` repo) and links |
| `14-stale-cache` | a `build/` from another source tree is regenerated |
| `15-mirror-headers` | library header install: siblings verbatim, others rewritten |
| `16-lua-script` | build-script `cmakeOptions` + pre/post `add_cmake` phases |
| `20-scaffold` | `forge create`, `forge new class/header/source/struct` |
| `21-doctor` | `forge doctor` layout report |
| `22-project-commands` | `project info/tree/stats/dependencies/scripts` |
| `23-scripts` | script listing, execution, exit-code propagation |
| `24-resources` | `forge embed` + generated embedded resources |
| `25-clean` | `forge clean` removes `build/` and is idempotent |
| `26-errors` | not-a-project and unknown-preset paths |
| `27-install` | `forge install` is a no-op without Conan |
| `28-presets` | flag presets + raw flags in the generated CMake |
| `30-fp-integration` | `../fp`: mirror byte-identical, git status unchanged *(skips)* |
| `31-fp-test` | `forge test` on `../fp` *(skips)* |

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
