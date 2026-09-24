# CLI Reference

Forge provides a set of commands to manage your C++ project's lifecycle.

## General Usage

```bash
forge [command] [options] [arguments]
```

---

## Project

Create, adopt and inspect the project itself.

### `create`
Creates a new Forge project.

**Usage:** `forge create <Name> [options]`

- `<Name>`: The name of the project.
- `--type <type>`: Type of project to create (`executable` or `library`). Defaults to `executable`.
- `--standard <std>`: C++ standard to write into `forge.lua` (default `20`).
- `--testing`: enable the test target (`testing = true`).

The new project gets a `.gitignore` that covers what Forge and the build
generate (`build/`, `compile_commands.json`, `CMakePresets.json`, …) and a
`.config/forge/definitions/definitions.lua` with the Lua API stubs for editors.

**Example:**
```bash
forge create MyProject --type library
```

---

### `init`
Sets up a Forge project in the **current** directory, keeping what is there.

**Usage:** `forge init [--name <name>] [--type executable|library] [--standard 20] [--testing]`

Where `create` makes a new directory, `init` adopts an existing one — an empty
folder or a checkout that already has sources. It writes the layout, a
`forge.lua` and (only when there is none) a `.gitignore`; existing files are
never overwritten, and it refuses to touch a directory that already has a
`forge.lua`.

```bash
cd existing-checkout
forge init --name mylib --type library --standard 17 --testing
forge build
```

---

### `new`
Parent command for creating new C++ entities.

#### `new class <Name>`
Generates a new C++ class (Header + Source).
**Example:** `forge new class Player`

#### `new struct <Name>`
Generates a new C++ struct.
**Example:** `forge new struct Vector3`

#### `new header <Name>`
Generates a new C++ header file.
**Example:** `forge new header Utils`

#### `new source <Name>`
Generates a new C++ source file.
**Example:** `forge new source main`

---

### `config`
Manages project configuration.

#### `config migrate`
Migrates an old `package.toml` configuration file to the new `forge.lua` format.

**Usage:** `forge config migrate`

---

### `project`
Parent command for project information and management.

#### `project info`
Displays a summary of the current project's configuration.

**Usage:** `forge project info`

#### `project tree`
Displays the project directory structure.

**Usage:** `forge project tree`

#### `project dependencies`
Lists all project dependencies defined in `forge.lua`.

**Usage:** `forge project dependencies`

#### `project scripts`
Lists all custom scripts defined in `forge.lua`.

**Usage:** `forge project scripts`

#### `project stats`
Displays statistics about the project, including file counts and total lines of code.

**Usage:** `forge project stats`

---

### `embed`
Registers a file in `resources.files` so the build embeds it.

**Usage:** `forge embed <path>`

The path is stored relative to the project. Re-embedding the same file reports
it instead of duplicating the entry; a missing file exits non-zero.

---

## Dependencies

Declare, resolve, pin and inspect what the project links.

### `add`
Adds or updates a dependency in `forge.lua`.

**Usage:** `forge add <name> (--git <url> --tag <ref> | --path <dir> | --conan <version> | --vcpkg <target> | --pkg-config) [--target <cmake-target>]`

```bash
forge add fmt --conan 10.2.1
forge add sdl --git https://github.com/libsdl-org/SDL.git --tag release-2.32.10 --target SDL2::SDL2
forge add forgefp --path ../fp --target forgefp
forge add fmt --vcpkg fmt::fmt
forge add zlib --pkg-config
```

Re-adding a name switches its channel. Local paths are checked up front, a git
dependency without `--tag` is rejected, and exactly one source must be given.
Run `forge install` afterwards to pin a git dependency in `forge.lock` (the
other channels are resolved by CMake during configure).

---

### `remove`
Removes a dependency (and its lock entry) from `forge.lua`.

**Usage:** `forge remove <name>`

---

### `install`
Resolves dependencies and writes `forge.lock`, and installs Conan packages when
the project declares them.

**Usage:** `forge install [--prefix <dir>] [--update]`

- `--prefix <dir>`: install the project itself into that directory (the CMake
  install tree: headers, library and package file) instead of only resolving
  dependencies.
- `--update`: re-resolve git refs and rewrite `forge.lock`, even when nothing
  in `forge.lua` changed.

`forge build` runs the dependency half of this automatically; run `forge install`
explicitly to pin new or changed dependencies without building, or in CI to
produce a reproducible lock. See
[Locking](dependency-management.md#locking-forgelock).

---

### `why`
Explains where a dependency comes from.

**Usage:** `forge why <name> [--json]`

Direct dependencies are read from `forge.lua` (reporting the channel: git, path,
conan, vcpkg or pkg-config). For anything else, Conan's resolved graph
(`conan graph info`) is walked upwards to name the package that pulls it in.

```bash
forge why fmt        # → transitive, required by spdlog/1.12.0 (direct dependency)
forge why spdlog     # → direct dependency (conan, version 1.12.0)
```

Exit code is non-zero when the name is neither declared nor in the graph.

---

### `outdated`
Shows git dependencies whose declared tag is behind the newest tag in the
repository.

**Usage:** `forge outdated [--json]`

Compares the declared ref against the newest semver-like tag found with
`git ls-remote --tags`, and reminds you to re-pin with `forge install` after
bumping a tag. Local `path` dependencies and Conan/vcpkg packages are not
checked (their versioning is not tag-based).

---

### `upgrade`
Bumps git dependencies to their newest version-like tag.

**Usage:** `forge upgrade [dependency...] [--apply] [--json]`

Without `--apply` it only reports (`forge outdated` shows the same comparison);
with it, the new tags are written to `forge.lua` and `forge.lock` is re-pinned by
running the install step — so the next build fetches the new commits.

```bash
forge upgrade                 # what could be bumped
forge upgrade --apply         # write the tags and re-pin the lock
forge upgrade fmt --apply     # only this dependency
```

---

### `vendor`
Copies fetched git dependencies into the project and switches them to local
`path` dependencies, so builds no longer need the network.

**Usage:** `forge vendor [--directory <dir>]`

Sources come from FetchContent's checkout in `build/_deps/<name>-src`, so run
`forge build` first. Vendored dependencies are snapshots (VCS metadata is not
copied) and their `forge.lock` entries are dropped — they are local now.

---

### `cache`
Inspects the shared cache of fetched git dependencies.

**Usage:** `forge cache list [--json]` · `forge cache clear [name]`

The cache lives in `$FORGE_CACHE_DIR`, else `$XDG_CACHE_HOME/forge/deps`, else
`~/.cache/forge/deps`. A dependency is cached by repository and reference and
handed to CMake as `FETCHCONTENT_SOURCE_DIR_<NAME>`, so the download is skipped —
which is why a warm cache also makes a build work offline. Only immutable
references are cached (a locked commit or a version-like tag): a branch moves,
and a cached copy would freeze it.

```bash
forge cache list                 # entries and their size
forge cache clear                # everything
forge cache clear sdl            # entries starting with "sdl"
```

---

## Build & Run

Configure, build, run and test.

### `build`
Generates CMake files and builds the project.

**Usage:** `forge build [options]`

- `--verbose`: Show verbose output from CMake.
- `--standard <version>`: C++ standard for this invocation (11, 14, 17, 20).
  Without it the standard from `forge.lua` is used (default 20).
- `--release` / `--debug`: Force the CMake build type.
- `--preset <names>`: Comma-separated build-flag presets for this invocation
  (e.g. `--preset asan,concurrency`). Adds to the `build.presets` list.
- `--no-config-presets`: Ignore `build.presets` from `forge.lua` entirely.
- `--jobs <n>`: Parallel build jobs (default: all cores). Without it builds use
  `--parallel`, so Makefile builds are no longer serial.

**Example:**
```bash
forge build --verbose --standard 17
forge build --release --preset warnings,lto
forge build --preset asan --no-config-presets
```

---

### `run`
Runs a custom script defined in `forge.lua` or the main executable if no script is specified.

**Usage:** `forge run [ScriptName] [arguments...] [build options]`

- `[ScriptName]`: Optional name of the script to run (from the `scripts` section
  in `forge.lua`). Extra words become positional parameters (`$1`, `$2`, …).
- *arguments*: with no script name, everything after `--` is passed to the
  program: `forge run -- --verbose file.txt`.
- Build options: `--release`/`--debug`/`--jobs`/`--standard`/`--preset`/
  `--no-config-presets`, plus `--no-build` to run the existing binary.
- `--bin <name>`: run a specific executable target (see `targets` in
  [Project Configuration](project-configuration.md#targets)).

**Example:**
```bash
forge run                                  # build + run
forge run --release -- --input data.txt    # release build, with arguments
forge run compile-shaders debug            # script, with $1 = debug
```

---

### `hot`
Builds the project for hot reload and runs it, reloading when sources change.

**Usage:** `forge hot [--manual] [--interval <seconds>] [--no-build] [--bin <name>] [-- program-arguments...]`

The build is a debug one with the pinned **jet-live** engine wired in, so
function bodies are replaced in the running process and its state — statics and
globals included — survives. A save is signalled to the process; a file added
or removed rebuilds first, because the generated compile commands change.

The application calls the generated glue: `forge_hot_init()` once, then
`forge_hot_update()` every iteration of its main loop.

```cpp
#include "forge_hot.h"

int main() {
  forge_hot_init();
  while (running) {
    forge_hot_update();
    frame();
  }
}
```

Hot mode is strict by design: `--release`, `unity = true`, `modules = true` and
the `asan`/`tsan`/`ubsan`/`msan`/`lto` presets are refused, because the engine
patches unoptimised code and loads a patch library into the process. The loop
must be single-threaded, and an object whose layout changed must be recreated in
the `forge_hot_pre_reload` / `forge_hot_post_reload` hooks — the engine is
silent about layout mismatches. See [Hot reload](build-system.md#hot-reload).

`--manual` watches nothing: send `SIGUSR1` to the printed pid to reload.
`--no-build` runs the existing binary. Linux and macOS only.

---

### `test`
Runs project tests (if enabled). Uses CTest when available and falls back to
the built test binary; the build step runs first.

**Usage:** `forge test [options]`

`--filter <pattern>` (or a suite name as the first argument) selects which tests
run; without one, the whole suite does.

Accepts the same build overrides as `forge build` (`--release`/`--debug`/
`--preset`/`--no-config-presets`), so you can, for example, run the suite under
sanitizers without changing `forge.lua`:

```bash
forge test --preset asan,ubsan --no-config-presets
```

Tests run in parallel (`-j`, all cores unless `--jobs` says otherwise), and
`--junit <path>` writes a JUnit XML report — resolved against the project
directory, so it does not land inside `build/`:

```bash
forge test --junit reports/tests.xml
forge test --json            # {"tests":…,"failures":…,"passed":…,"report":…}
```

A failing suite exits non-zero (and a failing build exits before the tests run),
so `forge test` works as a CI gate on its own. `--json` prints its summary as the
last line of the output.

---

### `bench`
Builds and runs the project's benchmark target (`<project>_bench`).

**Usage:** `forge bench [--no-build] [--save <file>] [--compare <file>] [--fail-over <percent>] [--json] [benchmark arguments...]`

Requires `testing = { benchmark = true }` and sources in `bench/`; extra
arguments are passed through to the binary (e.g. `--benchmark_filter=my_case`),
so Google Benchmark's own flags work as they are.

`--save` records the run as JSON (a baseline), `--compare` reports the deltas
against one, and `--fail-over` turns a regression into a failure — which is what
makes this usable as a performance gate in CI:

```bash
forge bench --save bench/base.json                     # record a baseline
forge bench --compare bench/base.json                  # what changed?
forge bench --compare bench/base.json --fail-over 10   # fail on >10% slower
forge bench --compare bench/base.json --json
```

```text
         vs bench/base.json
┌───────────┬──────────┬───────┬────────┐
│ Benchmark │ Baseline │ Now   │ Change │
├───────────┼──────────┼───────┼────────┤
│ BM_Work   │ 100 ns   │ 90 ns │ -10.0% │
└───────────┴──────────┴───────┴────────┘
```

Times are normalised to a common unit before comparing, and benchmarks that only
exist in one of the two runs are listed as new or gone.

---

### `watch`
Rebuilds (or re-tests) the project when its sources change.

**Usage:** `forge watch [--command build|test] [--interval <seconds>] [--iterations <n>] [build options]`

Watches `src`, `include`, `test` and `bench` (override with `--paths`), scanning
every `--interval` seconds. A newer, removed or added file triggers the command
again. `--iterations` counts runs including the first, so `--iterations 1` is
"build once and exit" — which is what makes it scriptable.

```bash
forge watch                       # rebuild on change
forge watch --command test        # re-run the suite on change
forge watch --release --interval 0.5
```

---

### `clean`
Cleans the build directory.

**Usage:** `forge clean`

Removes `build/` and the `compile_commands.json` symlink that points into it,
so editors do not keep reading a dangling link.

---

## Quality & Packaging

Format, lint and package.

### `format`
Formats the project's C++ sources (`src`, `test`, `bench`) with clang-format.

**Usage:** `forge format [--check] [paths...]`

Writes a `.clang-format` on first use (edit it freely). `--check` reports
instead of rewriting and exits non-zero when something is unformatted — the CI
form. Set `CLANG_FORMAT` to use a specific binary.

---

### `lint`
Runs clang-tidy over the project, using the compile database from `forge build`.

**Usage:** `forge lint [--fix] [--allow-warnings] [paths...]`

Writes a `.clang-tidy` on first use. Warnings fail the command by default (a CI
gate); `--allow-warnings` reports only. Set `CLANG_TIDY` to pick a binary.

Diagnostics are scoped to **this project's** headers (`src`, `include`, `test`,
`bench`) — otherwise every consumer of a header library would drown in warnings
it cannot fix. `--header-filter <regex>` overrides that, and a project that sets
`HeaderFilterRegex` in its own `.clang-tidy` keeps control.

---

### `publish`
Packages the project with CPack.

**Usage:** `forge publish [--format <TGZ|ZIP|DEB|RPM|…>] [--output <dir>] [--no-build] [--config <cfg>]`

Requires `project.version` (it names the archive). The build runs first unless
`--no-build` is given, then `cpack` writes into `dist/` (or `--output`). Forge
reports the archives it produced and exits non-zero when `cpack` fails.

```bash
forge publish --format TGZ --output out
forge publish --format DEB --config Release   # multi-config generators
```

---

## CI & Workspaces

Generate CI, and build several projects together.

### `ci`
Generates a CI workflow for the project, tailored to what the configuration
actually needs.

**Usage:** `forge ci [--provider github|gitlab] [--runners <list>] [--configurations <list>] [--force] [--check]`

The generated file reflects the project: the dependency channels in use add
their setup steps (Conan, vcpkg, pkg-config), `testing` adds `forge test`, a
`.clang-tidy`/`.clang-format` adds the lint/format steps, and a versioned
project packages and uploads artifacts. The default matrix is
`ubuntu-latest, macos-latest` × `release, debug`.

The output is deterministic, so `--check` fails when the workflow has drifted
from the configuration — useful as a CI step of its own. An existing file is
never overwritten without `--force`.

```bash
forge ci                                   # .github/workflows/forge.yml
forge ci --provider gitlab                 # .gitlab-ci.yml
forge ci --check                           # fail when out of date
forge ci --runners ubuntu-latest --force
```

---

### `workspace`
Runs a command across several sibling projects, in dependency order.

**Usage:**

```bash
forge workspace list                  # projects, versions, dependencies
forge workspace build [project...]    # build everything (or some) in order
forge workspace test  [project...]    # test everything (or some) in order
```

The workspace root is the nearest ancestor with a `forge.workspace.lua`;
without one it is the directory containing the current project. Members are
discovered from the subdirectories holding a `forge.lua`. Naming a project
builds it plus its local dependencies, and `--dry-run` prints the order without
running anything. See [Workspaces](workspaces.md).

---

## Environment & Utilities

Check or install the tools Forge drives, and the download helpers.

### `setup`
Checks the tools Forge drives and can install what is missing.

**Usage:** `forge setup [--install] [--yes] [--dry-run] [--tools <list>]`

Without `--install` it only reports: each tool, whether it is present, its
version and what it is for. The exit code is non-zero when a **required** tool
is missing (git, CMake ≥ 3.23, a C++ compiler), which makes it usable as a CI
gate.

`--install` runs the machine's own package manager (apt-get, dnf, zypper, apk,
pacman, brew, winget, choco), asking for confirmation unless `--yes` is given.
It lists the exact command and uses the system's own `sudo` prompt — Forge never
handles a password. `--dry-run` prints the plan for the selected tools without
running anything.

`--json` prints the same report machine-readably (one object per tool with
`installed`, `required`, `version` and the detected `packageManager`).

```bash
forge setup                          # what is installed, what is missing
forge setup --install                # install the missing tools
forge setup --install --dry-run      # just show the commands
forge setup --tools cmake,ninja
```

The ecosystem tools are not packages, so they have steps of their own — select
them with `--tools` and `--install` runs them (asking first, `--yes` to skip):

```bash
forge setup --tools conan,vcpkg
forge setup --install --yes --tools conan,vcpkg
```

- **conan** — installed with the machine's package manager (`brew install
  conan`, …), or through `pipx` where the archive has no Conan 2 (Arch) or only
  1.x (Debian/Ubuntu).
- **vcpkg** — its own prerequisites first (`curl`, `zip`, `unzip`, `tar`), then
  cloned and bootstrapped into `external/vcpkg` when run inside a project (which
  is where Forge looks for it, no environment variable needed), or into
  `~/.local/share/forge/vcpkg` otherwise, printing the `VCPKG_ROOT` export to
  use it. A checkout that exists but is not bootstrapped is bootstrapped rather
  than cloned again.

Both channels are documented in [Dependency Management](dependency-management.md).

Without `--tools`, `--install` offers **both** ecosystems — Conan and vcpkg —
so a fresh machine is ready for whatever comes next. Each is confirmed
separately (vcpkg is a checkout, Conan a package), and the output says whether
the current project actually uses it, so an irrelevant one costs a keystroke to
skip. `--tools conan` narrows it to one.

A tool that is installed but **not on `PATH`** is reported separately, with the
fix. `--install` writes it: the missing `PATH` entry (or `VCPKG_ROOT` export) is
appended to your shell profile (`~/.bashrc`, `~/.zshrc`, fish's `config.fish`,
or `~/.profile`), once — every entry is marked with a comment and an entry
already present is left alone. Nothing else in the file is touched, and a new
shell is needed to pick it up.

!!! warning "Installed but unusable"

    A symlink pointing at a missing target (a `pipx install` that ran as root),
    or a pipx venv whose command was never exposed, is reported separately with
    the fix — `pipx install --force`, or removing the stale venv and
    reinstalling.

`forge doctor` reports the same table and the same PATH problems, and `forge ci`
uses it to write the install steps of the workflow it generates.

---

### `doctor`
Checks the environment for missing dependencies (CMake, Conan, etc.).

**Usage:** `forge doctor [--fix] [--json]`

Reports, in order: the configuration, the directory layout, dependencies,
conflicts, resources, scripts, features, whether the generated files are
ignored by git, and the **toolchain** (each tool with its version, plus how to
install what is missing — see `forge setup`).

`--fix` creates missing directories, appends missing `.gitignore` entries, and
refreshes the Lua editor stubs in `.config/forge/definitions/`.

`--json` prints the same checks machine-readably (project, layout, `.gitignore`,
toolchain, dependency counts) as the *only* output, and exits non-zero when a
required tool or directory is missing — so CI can gate on it:

```bash
forge doctor --json | python3 -m json.tool
```

---

### `download`
Downloads a file over HTTP(S), optionally verifying its SHA256.

**Usage:** `forge download <url> -o <path> [--sha-256 <hash>] [--timeout <seconds>] [--show-progress]`

```bash
forge download https://example.com/sdk.zip -o sdk.zip --sha-256 <hash>
```

A failed request, or a hash that does not match, exits non-zero and removes the
partial file. (Dependencies from `dependencies.direct` are fetched by CMake
during `forge build` — this command is for custom setup steps.)

`--show-progress` draws a live bar only when the output is a terminal; when it
is captured (CI logs, a pipe) the download runs without the display. A failed
download — a dropped connection or a hash mismatch — removes the output file, so
a truncated file is never mistaken for a complete one.

Without `--output`, the file name comes from the URL (`curl -O` style); a URL
with no file name asks for one.

---

### `extract`
Extracts a `.zip`, `.tar`, `.tgz` or `.tar.gz` archive.

**Usage:** `forge extract <archive> <output-dir> [--strip-components <n>]`

`--strip-components` defaults to `1`, so an archive with a single top-level
directory (the shape GitHub produces) unpacks its contents directly into
`<output-dir>`. Pass `0` to keep the original layout.

---

### `fetch`
Downloads and extracts an archive in one step.

**Usage:** `forge fetch <url> <output-dir> [--strip-components <n>] [--sha-256 <hash>]`

Same extraction rules as `extract`; the archive type is taken from the URL.

---

### `completions`
Prints a shell completion script.

**Usage:** `forge completions <bash|zsh|fish>`

```bash
forge completions bash > /etc/bash_completion.d/forge
forge completions zsh  > "${fpath[1]}/_forge"
forge completions fish > ~/.config/fish/completions/forge.fish
```
