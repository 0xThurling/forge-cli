# Dependency Management

Forge resolves dependencies through **five channels**, all declared in
`forge.lua` and all ending up in the generated CMake as targets you link.

| Channel | Declared under | Resolved by | Good for |
|---|---|---|---|
| **git** | `dependencies.direct` | CMake `FetchContent`, pinned by `forge.lock` | libraries built from source alongside your project |
| **path** | `dependencies.direct` | CMake `FetchContent` with `SOURCE_DIR` | sibling checkouts you are editing at the same time |
| **conan** | `dependencies.conan` | the `conan` executable, during `forge build`/`install` | large or binary dependencies, version solving |
| **vcpkg** | `dependencies.vcpkg` | vcpkg's toolchain, during CMake configure | Windows/CI, curated ports |
| **pkg-config** | `dependencies.pkgconfig` | CMake's `PkgConfig` module | system libraries that ship a `.pc` file |

Two constraints: **Conan and vcpkg both set `CMAKE_TOOLCHAIN_FILE`**, and an
explicit `build.toolchain_file` cannot be combined with either. Forge reports
the conflict instead of silently picking one.

## A complete project

```lua
-- forge.lua
return {
  project = {
    name = "app", type = "executable", standard = "20", version = "0.1.0",
  },
  dependencies = {
    direct = {
      -- built from source, tests off, pinned by forge.lock
      fmt = {
        git = "https://github.com/fmtlib/fmt.git",
        tag = "10.2.1",
        target = "fmt::fmt",
        options = { FMT_TEST = "OFF" },
      },
      -- a sibling checkout, edited together with this project
      forgefp = { path = "../fp", target = "forgefp" },
    },
    conan = {},                       -- or: { spdlog = "1.12.0" }
    vcpkg = {},                       -- or: { sdl2 = "SDL2::SDL2" }
    pkgconfig = { "zlib" },           -- system libraries
  },
  resources = { files = {} },
  scripts = {},
  features = {},
}
```

```bash
forge install          # resolve fmt → a commit in forge.lock
forge build            # fetch, build, link
forge run
```

What that produces, in `.config/cmake/CMakeLists.txt` (a real excerpt):

```cmake
find_package(PkgConfig REQUIRED)
pkg_check_modules(ZLIB REQUIRED IMPORTED_TARGET zlib)

include(FetchContent)
FetchContent_Declare(fmt GIT_REPOSITORY "https://github.com/fmtlib/fmt.git" GIT_TAG "10.2.1")
set(FMT_TEST "OFF" CACHE STRING "" FORCE)
FetchContent_MakeAvailable(fmt)
FetchContent_Declare(forgefp SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../fp")
FetchContent_MakeAvailable(forgefp)

target_link_libraries(app PRIVATE fmt::fmt forgefp PkgConfig::ZLIB)
```

## The lifecycle

```text
forge.lua        declare        (channels, tags, options)
forge install    resolve        (git refs → commits in forge.lock; conan install)
forge build      configure      (FetchContent / toolchain / pkg-config → targets)
forge test       link           (tests link the same dependencies)
```

- `forge install` is the only command that **resolves** (writes `forge.lock`).
- `forge build` only *uses* what is declared and locked, so it needs no network
  beyond FetchContent's own fetch — and none at all once the cache is warm.

```bash
forge project dependencies              # what the project ends up with
forge project dependencies --json
```

```text
┌─────────┬───────────────────────────────────┬────────┬──────────┐
│ Name    │ Source                            │ Ref    │ Target   │
├─────────┼───────────────────────────────────┼────────┼──────────┤
│ fmt     │ https://github.com/fmtlib/fmt.git │ 10.2.1 │ fmt::fmt │
│ forgefp │ path:../fp                        │        │ forgefp  │
└─────────┴───────────────────────────────────┴────────┴──────────┘
```

```json
[{"name":"fmt","channel":"git","source":"https://github.com/fmtlib/fmt.git","ref":"10.2.1","target":"fmt::fmt"},
 {"name":"forgefp","channel":"path","source":"../fp","ref":"","target":"forgefp"}]
```

## The dependency model

`dependencies.direct.<key>` accepts:

| Field | Type | Example | Meaning |
|---|---|---|---|
| `git` | string | `"https://github.com/fmtlib/fmt.git"` | repository URL; requires `tag` |
| `tag` | string | `"10.2.1"`, `"main"`, `"a1b2c3d"` | tag, branch or commit to fetch |
| `path` | string | `"../fp"` | local directory instead of `git` |
| `target` | string | `"fmt::fmt"` | CMake target to link; defaults to the key |
| `export` | table | `{ package = "forgefp", target = "forgefp::forgefp" }` | travel with an installed package (below) |
| `options` | table | `{ FMT_TEST = "OFF" }` | CMake cache variables for the dependency |

Each field on its own:

```lua
direct = {
  -- minimal git dependency: the key is also the target
  fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" },

  -- a pinned commit instead of a tag (reproducible without a lock)
  glm = { git = "https://github.com/g-truc/glm.git", tag = "0f9a6b1c4e5d7a8b9c0d1e2f3a4b5c6d7e8f9a0b" },

  -- the CMake target differs from the key
  sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.32.10",
          target = "SDL2::SDL2" },

  -- a local checkout
  forgefp = { path = "../fp", target = "forgefp" },

  -- the dependency's own tests off
  spdlog = { git = "https://github.com/gabime/spdlog.git", tag = "v1.14.1",
             target = "spdlog::spdlog", options = { SPDLOG_BUILD_TESTS = "OFF" } },
}
```

A dependency needs **`git` with `tag`, or `path`** — anything else is reported
and ignored rather than put on the link line:

```text
Warning: dependency `broken` has no source — give it `git` with `tag`, or `path`. Ignoring it.
Warning: `dependencies.direct.direct` looks like an extra nesting level — dependencies belong directly under `direct`. Ignoring it.
```

## Installed packages and their dependencies

A library installs a CMake package, and that package cannot point at targets
that only exist in this build. For a dependency that is installable itself, say
how consumers find it:

```lua
direct = {
  forgefp = { path = "../fp", target = "forgefp",
              export = { package = "forgefp", target = "forgefp::forgefp" } },
}
```

The generated `<project>Config.cmake` then calls `find_dependency(forgefp)`
before loading the targets, and the installed interface links
`forgefp::forgefp` instead of the build-tree target — a consumer only has to
`find_package(<project>)`.

Without `export`, a dependency stays in the build tree: the installed package
cannot reference it, and the build says so.

```text
Warning: forgefp is not exported by this project: the installed package will not propagate it.
```

`export` applies to direct (`git`/`path`) dependencies. Conan, vcpkg and
pkg-config packages are resolved by the consumer's own toolchain.

## Git dependencies

**Declare** (above), then:

```bash
forge install                 # resolve the tag to a commit, write forge.lock
forge build                   # fetch that commit, build, link
```

**What Forge emits** — with a lock, the commit; without, the declared ref:

```cmake
FetchContent_Declare(fmt GIT_REPOSITORY "https://github.com/fmtlib/fmt.git" GIT_TAG "6f8c1d2…")
FetchContent_MakeAvailable(fmt)
```

**Use it in C++** — the dependency's own include paths come with its target:

```cpp
#include <fmt/core.h>

int main() {
  fmt::print("hello {}\n", 42);
}
```

**Keep it current:**

```bash
forge outdated
```
```text
┌─────────┬──────────┬────────────┐
│ Name    │ Declared │ Newest tag │
├─────────┼──────────┼────────────┤
│ fmt     │ 10.2.1   │ 11.0.2     │
└─────────┴──────────┴────────────┘

forge upgrade --apply
# Updated 1 tag(s) in forge.lua.
# Locked 1 dependency in forge.lock.
```

```bash
forge upgrade                 # report only
forge upgrade fmt --apply     # one dependency
forge outdated --json         # [{"name":"fmt","current":"10.2.1","latest":"11.0.2"}]
```

## Path dependencies

```lua
direct = { forgefp = { path = "../fp", target = "forgefp" } }
```

```cmake
FetchContent_Declare(forgefp SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../fp")
FetchContent_MakeAvailable(forgefp)
```

```cpp
#include <forgefp/fp/all.hpp>   // the sibling's headers, used in place

int main() { return fp::out(fp::into(21) | [](int x) { return x * 2; }) == 42 ? 0 : 1; }
```

- Relative paths resolve against the project directory; absolute paths are used
  as-is. The emitted path stays relative, so the generated CMake is portable
  across machines sharing the layout.
- The dependency must have been **built at least once** — Forge writes a
  project's `CMakeLists.txt` when that project is built, and CMake silently
  skips a subdirectory without one:

  ```
  Warning: Dependency 'forgefp' at '../fp' has no CMakeLists.txt yet — run `forge build` in it first (or `forge workspace build`).
  ```
- A wrong path is reported with the resolved location:

  ```
  Warning: Dependency 'forgefp' points at '../nope', which does not exist (expected '/home/you/C++/nope').
  ```
- Path dependencies are not locked — they are whatever is on disk. Switch to
  `git`/`tag` for CI or for consumers that do not share the layout.

## Conan packages

### Declaring packages

```lua
dependencies = {
  direct = {},
  conan = {
    nlohmann_json = "3.11.2",
    spdlog = "1.12.0",
  },
}
```

### What Forge generates

**The manifest** — `.config/conanfile.txt`:

```ini
[requires]
nlohmann_json/3.11.2
spdlog/1.12.0

[generators]
CMakeDeps
CMakeToolchain

[layout]
cmake_layout
```

**The command** (the configuration being built is forwarded):

```bash
conan install .config/conanfile.txt --output-folder=build --build=missing -s build_type=Release
```

**The link lines**, from the summary Conan prints:

```cmake
find_package(spdlog REQUIRED)
target_link_libraries(app PRIVATE spdlog::spdlog)
```

### In your code

```cpp
#include <spdlog/spdlog.h>

int main() { spdlog::info("hello from conan"); }
```

### Where things are

| Artifact | Path |
|---|---|
| the manifest Forge writes | `.config/conanfile.txt` |
| Conan's toolchain (passed to CMake) | `build/build/<Configuration>/generators/conan_toolchain.cmake` |
| per-package CMake configs | `build/build/<Configuration>/generators/<pkg>/<pkg>-config.cmake` |
| Conan's own lock | `build/build/<Configuration>/generators/conan.lock` |

### Requirements and diagnostics

Requires the `conan` executable — `forge setup` reports it, and its hint is
manager-aware, and `forge setup --install --tools conan` installs it for you
(`pipx` where the archive has no Conan 2 (Arch) or only 1.x (Debian/Ubuntu)). A
missing binary is reported, not guessed at:

```text
Error: Conan is required by dependencies.conan but could not be run.
```

Transitive packages come along automatically. Ask where one came from:

```bash
forge why fmt
```
```text
fmt
   transitive fmt/10.2.1#b1c2d3
   required by spdlog/1.12.0 (direct dependency)
```

`forge doctor` reports conflicts between a Conan package and a git dependency of
the same library:

```text
⚠️  Dependency Conflicts Detected:
   - <what Conan reported, e.g. a duplicate package from two channels>
```

### Not supported yet

Per-package Conan options and settings (for example
`-o fmt/*:shared=True`). The table takes a version only — there is no
declarative place to put them, so say something if you need this.

## Using vcpkg (manifest mode)

### Declaring packages

```lua
dependencies = {
  direct = {},
  conan = {},
  vcpkg = {
    fmt = "fmt::fmt",                                          -- shorthand
    spdlog = { target = "spdlog::spdlog", version = "1.12.0" },
  },
}
vcpkg_root = "external/vcpkg"        -- or $VCPKG_ROOT
vcpkg_baseline = "<commit>"          -- optional builtin-baseline
vcpkg_triplet = "x64-mingw-static"   -- optional target triplet
```

### What Forge generates

**The manifest** — `vcpkg.json`:

```json
{
  "name": "ex-c",
  "version-string": "0.1.0",
  "builtin-baseline": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
  "dependencies": [
    { "name": "sdl2", "version>=": "2.32.10" }
  ]
}
```

**The link lines** — the declared target is linked, because vcpkg cannot be
queried for targets without running it:

```cmake
find_package(SDL2 REQUIRED)
target_link_libraries(app PRIVATE SDL2::SDL2)
```

### In your code

```cpp
#include <SDL2/SDL.h>

int main() { SDL_Init(SDL_INIT_VIDEO); SDL_Quit(); }
```

### Getting vcpkg

If you do not have a checkout yet, let `forge setup` do it:

```bash
forge setup --install --tools vcpkg
# git clone --depth 1 https://github.com/microsoft/vcpkg external/vcpkg
# cd external/vcpkg && ./bootstrap-vcpkg.sh
```

It lands in `external/vcpkg` when run inside the project, which is exactly where
Forge looks, so no environment variable is needed. vcpkg's bootstrap needs
`curl`, `zip`, `unzip` and `tar`; when one is missing, the failure names the
command that installs it for your machine.

### Toolchain and triplet

The toolchain is handed to CMake (`CMAKE_TOOLCHAIN_FILE` → vcpkg's), and
`vcpkg_triplet` becomes `VCPKG_TARGET_TRIPLET` in the cache, so dependencies are
built for that platform. A missing checkout is reported with the three ways to
provide one:

```text
Error: vcpkg dependencies are declared but no vcpkg checkout was found — set
`vcpkg_root` in forge.lua, export `VCPKG_ROOT`, or clone it into `external/vcpkg`.
```

## Using pkg-config

```lua
dependencies = { direct = {}, conan = {}, pkgconfig = { "gtk+-3.0", "zlib" } }
```

**What Forge emits** — one imported target per module:

```cmake
find_package(PkgConfig REQUIRED)
pkg_check_modules(ZLIB REQUIRED IMPORTED_TARGET zlib)
pkg_check_modules(GTK3 REQUIRED IMPORTED_TARGET gtk+-3.0)

target_link_libraries(app PRIVATE PkgConfig::ZLIB PkgConfig::GTK3)
```

The target name is the module upper-cased (`zlib` → `PkgConfig::ZLIB`).

**Use it** — include paths and definitions come from the `.pc` file, so no extra
configuration is needed:

```cpp
#include <zlib.h>

int main() { return zlibVersion() == nullptr; }
```

```bash
sudo pacman -S zlib            # or: apt-get install zlib1g-dev
forge build
```

A missing module fails the configure with pkg-config's own message; set
`PKG_CONFIG_PATH` for modules installed outside the default search path.

## Per-dependency CMake options

Emitted as `set(<NAME> "<value>" CACHE STRING "" FORCE)` **before**
`FetchContent_MakeAvailable`, which is the only point where a fetched project's
own `option()` calls can still see them:

```lua
direct = {
  fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1",
          target = "fmt::fmt", options = { FMT_TEST = "OFF" } },
  sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.32.10",
          target = "SDL2::SDL2", options = { SDL_TEST = "OFF", SDL_EXAMPLES = "OFF" } },
}
```

```cmake
FetchContent_Declare(sdl GIT_REPOSITORY "https://github.com/libsdl-org/SDL.git" GIT_TAG "release-2.32.10")
set(SDL_TEST "OFF" CACHE STRING "" FORCE)
set(SDL_EXAMPLES "OFF" CACHE STRING "" FORCE)
FetchContent_MakeAvailable(sdl)
```

The proof it took effect is in the cache:

```bash
grep SDL_TEST build/CMakeCache.txt
# SDL_TEST:STRING=OFF
```

Common uses: switching off a dependency's tests and examples (the big win for
SDL, which otherwise builds both), picking a backend, or disabling a feature you
do not link against.

## Dependencies and their own tests

A dependency is built for the consumer, but it does not drag its test suite
along: the generated test and benchmark blocks are guarded with
`CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR`, so they only run when the
project is the top-level one. The test framework is fetched inside that guard
too, so consuming a tested library does not download GoogleTest.

```bash
forge build                                   # consuming ForgeFP: no GoogleTest fetch
forge build -DFORGE_BUILD_DEPENDENCY_TESTS=ON # opt in, when you want them
```

Packaging is guarded the same way: a dependency's CPack configuration never
leaks into the consumer's build.

## Locking (`forge.lock`)

Git dependencies are declared with a tag or branch, and those move. `forge
install` resolves each one to a commit and writes `forge.lock`:

```json
{
  "version": 1,
  "git": {
    "fmt": {
      "git": "https://github.com/fmtlib/fmt.git",
      "tag": "10.2.1",
      "commit": "6f8c1d2e3b4a5968778695a4b3c2d1e0f9a8b7c6"
    }
  }
}
```

**Commit it.** Every machine and CI run then builds the same sources.

```bash
forge install                 # resolve new or changed dependencies
forge install --update        # re-resolve everything (after bumping a tag yourself)
```

- Changing a dependency's `git` or `tag` **invalidates** its entry: the next
  `forge install` re-resolves it.
- `forge vendor` drops the entries it vendored, because those are local now.
- Local `path` dependencies are not locked.

!!! note "Only `forge install` writes the lock"

    `forge build` never does, so a build never resolves a ref — it uses what is
    already declared and locked.

## The shared cache

A fetched git dependency is cloned **once** into a shared cache and handed to
CMake as `FETCHCONTENT_SOURCE_DIR_<NAME>`, so a second project — or a second CI
run — skips the download entirely.

```cmake
set(FETCHCONTENT_SOURCE_DIR_FMT "/home/you/.cache/forge/deps/fmt-3f9c1a2b" CACHE PATH "" FORCE)
```

```bash
forge cache list
```
```text
┌──────────────────┬────────┐
│ Entry            │ Size   │
├──────────────────┼────────┤
│ fmt-3f9c1a2b     │ 12.4 MiB │
│ sdl-531edbf7     │ 88.1 MiB │
└──────────────────┴────────┘

forge cache clear                # everything
forge cache clear sdl            # entries starting with "sdl"
```

- Location: `$FORGE_CACHE_DIR`, else `$XDG_CACHE_HOME/forge/deps`, else
  `~/.cache/forge/deps`.
- Keyed by repository **and** reference, so a bumped tag is a new entry.
- Only immutable references are cached — a locked commit, a version-like tag, or
  a tag with a dotted version in it (`release-2.32.10`). Branches are never
  cached, because a cached copy would freeze a moving reference.
- `build.cache = "off"` or `FORGE_NO_CACHE=1` disables it:

  ```bash
  FORGE_NO_CACHE=1 forge build
  ```

## Offline builds

| Approach | Command | Result |
|---|---|---|
| **Vendor** | `forge vendor` | copies each fetched dependency into `external/<name>`, rewrites `forge.lua` to `path` dependencies, drops the lock entries |
| **Cache** | (nothing) | a warm `~/.cache/forge/deps` serves the build; clearing it needs the network again |

```bash
forge build            # fetches into the cache
forge vendor
# Vendored fmt → external/fmt
# Vendored 1 dependency into `external`. Builds no longer need the network.

rm -rf ~/.cache/forge/deps   # even with the cache gone…
forge build                  # …the vendored sources are local
```

`forge.lua` afterwards:

```lua
direct = { fmt = { path = "external/fmt", target = "fmt::fmt" } }
```

Vendored dependencies are snapshots: VCS metadata is not copied, so they no
longer update with `forge upgrade` — switch them back to `git` when you want
that.

## Recipes

### A library from source, tests off

```lua
direct = { fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1",
                   target = "fmt::fmt", options = { FMT_TEST = "OFF" } } }
```

### A dependency whose CMake target differs

SDL exposes `SDL2::SDL2`, so the key can be anything:

```lua
direct = { sdl = { git = "https://github.com/libsdl-org/SDL.git",
                   tag = "release-2.32.10", target = "SDL2::SDL2",
                   options = { SDL_TEST = "OFF", SDL_EXAMPLES = "OFF" } } }
```

### Another Forge project beside yours

ForgeFP, a library: build it once, then point at it — no `install` step, headers
used in place.

```lua
direct = { forgefp = { path = "../fp", target = "forgefp" } }
```

### A test framework

A test framework is a normal git dependency, but Forge manages it for you when
`testing = true` — see [Project Configuration](project-configuration.md).

```lua
-- what Forge writes into forge.lua for you
direct = { googletest = { git = "https://github.com/google/googletest.git", tag = "v1.14.0" } }
```

### A system library

No build, no fetch:

```lua
dependencies = { direct = {}, conan = {}, pkgconfig = { "openssl", "zlib" } }
```

### A prebuilt, version-solved library

```lua
dependencies = { direct = {}, conan = { boost = "1.85.0" } }
```

### A mixed project

The common shape for a game or tool:

```lua
dependencies = {
  direct = {
    forgefp = { path = "../fp", target = "forgefp" },          -- your own library
    sdl    = { git = "https://github.com/libsdl-org/SDL.git",
               tag = "release-2.32.10", target = "SDL2::SDL2",
               options = { SDL_TEST = "OFF", SDL_EXAMPLES = "OFF" } },
  },
  conan = {},                     -- nothing here: SDL already owns the toolchain
  pkgconfig = { "zlib" },
}
```

## CI

Commit `forge.lock`, and cache the shared dependency cache between runs:

```yaml
      - name: Cache Forge dependencies
        uses: actions/cache@v4
        with:
          path: ~/.cache/forge/deps
          key: forge-deps-${{ hashFiles('forge.lock') }}

      - name: Install Forge
        run: curl -sSL https://raw.githubusercontent.com/0xThurling/forge-cli/refs/heads/main/install.sh | bash

      - name: Build and test
        run: |
          forge setup --install --yes     # the tools this project needs
          forge build
          forge test --junit reports/tests.xml
```

`forge ci` writes a workflow like this for the project, including the
channel-specific setup steps (Conan, vcpkg, pkg-config).

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Warning: Skipping invalid dependency 'x'` / `dependency 'x' has no source` | no `git`+`tag` and no `path` | give it a source; Forge ignores it rather than linking a missing target |
| `Warning: \`dependencies.direct.direct\` looks like an extra nesting level` | one `direct` too many in `forge.lua` | dependencies belong directly under `direct`; any config rewrite (`forge add`/`remove`) drops the bad level |
| `Warning: Dependency 'x' at '../x' has no CMakeLists.txt yet` | the path dependency was never built | `forge build` in it once, or `forge workspace build` |
| `Warning: Dependency 'x' points at '…', which does not exist` | wrong relative path | the message shows the resolved path |
| `Error: \`toolchain_file\` cannot be combined with Conan or vcpkg` | both set `CMAKE_TOOLCHAIN_FILE` | pick one channel per project |
| `Error: vcpkg dependencies are declared but no vcpkg checkout was found` | `vcpkg_root`/`$VCPKG_ROOT` unset and `external/vcpkg` missing | set one, or clone vcpkg and bootstrap it |
| `Error: Conan is required by dependencies.conan but could not be run` | `conan` not on `PATH` | `forge setup --install --tools conan` |
| `cannot find -l<name>` after editing `forge.lua` | an entry with no source used to reach the link line | fixed: invalid entries are reported and skipped |
| `find_package(<pkg>)` fails during configure | the dependency is declared but not installed for this channel | `forge install` (conan) / check `vcpkg_root` (vcpkg) |
| A dependency's tests are being built | it is the top-level project, or the override is set | nothing to do; `-DFORGE_BUILD_DEPENDENCY_TESTS=ON` is the opt-in |

## Command reference

| Command | Example |
|---|---|
| `forge install [--prefix <dir>] [--update]` | `forge install --prefix out` |
| `forge add <name> --git <url> --tag <ref>` | `forge add fmt --git https://github.com/fmtlib/fmt.git --tag 10.2.1` |
| `forge add <name> --path <dir>` | `forge add forgefp --path ../fp --target forgefp` |
| `forge add <name> --conan <version>` | `forge add spdlog --conan 1.12.0` |
| `forge add <name> --vcpkg <target>` | `forge add sdl2 --vcpkg SDL2::SDL2` |
| `forge add <name> --pkg-config` | `forge add zlib --pkg-config` |
| `forge remove <name>` | `forge remove fmt` |
| `forge outdated [--json]` | `forge outdated --json` |
| `forge upgrade [<name>] [--apply]` | `forge upgrade fmt --apply` |
| `forge vendor [--directory <dir>]` | `forge vendor --directory third_party` |
| `forge cache list` / `forge cache clear [name]` | `forge cache clear sdl` |
| `forge why <name> [--json]` | `forge why spdlog` |
| `forge project dependencies [--json]` | `forge project dependencies --json` |
