# Dependency Management

Forge supports two primary ways to manage your project's dependencies: **Direct Git Dependencies** (via CMake FetchContent) and **Conan Packages**.

![Raylib Workflow Example](https://raw.githubusercontent.com/0xThurling/forge-cli/refs/heads/main/.branding/workflow.PNG)

## Direct Git Dependencies

Direct dependencies are managed by Forge using CMake's `FetchContent` module. This is ideal for libraries that are available on GitHub and can be built alongside your project.

### Configuration

Add your dependencies to the `dependencies.direct` table in `forge.lua`:

```lua
dependencies = {
    direct = {
        ["fmt"] = {
            git = "https://github.com/fmtlib/fmt.git",
            tag = "10.1.1" -- Can be a tag, branch, or commit hash
        },
        ["glm"] = {
            git = "https://github.com/g-truc/glm.git",
            tag = "0.9.9.8"
        }
    }
}
```

### Usage

1. Run `forge download` to fetch the dependencies into your project.
2. Run `forge build`. Forge will automatically generate the `FetchContent_Declare` and `FetchContent_MakeAvailable` calls in your `CMakeLists.txt`.
3. Link the dependency in your code. Forge handles the target linking if it can identify the target name, otherwise, you might need to use `forge.add_cmake` to manually link.

---

## Local Path Dependencies

When the dependency lives beside your project — a workspace of sibling
checkouts — point at it directly instead of going through Git:

```lua
dependencies = {
    direct = {
        ["forgefp"] = {
            path = "../fp",       -- relative to the project directory
            target = "forgefp"    -- the CMake target to link
        }
    }
}
```

Forge emits
`FetchContent_Declare(forgefp SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../fp")`,
so the dependency is configured and built from that directory with no clone,
fetch, or tag round-trip. Edits to the dependency are picked up on the next
`forge build`.

- Relative paths resolve against the project directory; absolute paths are
  used as-is.
- The emitted path is relative to the project, so the generated CMake stays
  portable across machines that share the layout.
- A missing directory is reported at generation time, with the resolved path,
  instead of surfacing later as a confusing CMake error.
- A path dependency that has **never been built** is reported too: Forge writes
  a project's `CMakeLists.txt` when that project is built, and CMake silently
  skips a subdirectory without one. Run `forge build` in the dependency first,
  or let [`forge workspace build`](workspaces.md) order it for you.
- `path` and `git`/`tag` are interchangeable per dependency: use `path` while
  developing side by side, and `git`/`tag` for CI or for consumers that don't
  share the layout.
- The dependency **key** is the `FetchContent` name while `target` is what gets
  linked, so they may differ (a key of `myfoo` linking a target `foo`).

---

## Dependency options

A dependency can be configured with CMake cache variables, which is how you
turn off the parts of it you do not need:

```lua
direct = {
    fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1",
            target = "fmt::fmt", options = { FMT_TEST = "OFF" } },
    sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.32.10",
            target = "SDL2::SDL2", options = { SDL_TEST = "OFF", SDL_EXAMPLES = "OFF" } }
}
```

They are emitted as `set(<NAME> "<value>" CACHE STRING "" FORCE)` immediately
before `FetchContent_MakeAvailable`, so the dependency's own `option()` calls
see them. (A plain `set()` would run after those calls and be ignored.)

## Dependencies and their own tests

A dependency is built for the consumer, but it does not drag its test suite
along: the generated test (and benchmark) block is guarded with
`CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR`, so it only runs when the
project is the top-level one. The test framework is fetched inside that guard
too, so consuming a tested library does not download GoogleTest.

Set `-DFORGE_BUILD_DEPENDENCY_TESTS=ON` to override this (for example when
building a dependency's tests on purpose). Packaging is guarded the same way: a
dependency's CPack configuration never leaks into the consumer's build.

## The shared cache

A fetched git dependency is cloned into a shared cache (keyed by repository and
reference) and handed to CMake as `FETCHCONTENT_SOURCE_DIR_<NAME>`, so a second
project — or a second CI run — skips the download. It also means a build works
offline once the cache is warm. `forge cache list` and `forge cache clear` manage
it; `build.cache = "off"` (or `FORGE_NO_CACHE=1`) turns it off. Branches are
never cached, because a cached copy would freeze a moving reference.

## Locking (`forge.lock`)

Git dependencies are declared with a tag or branch, and those move. `forge
install` resolves each git dependency to a commit and writes `forge.lock`; the
generated CMake then fetches that exact commit, so every machine and CI run
builds the same sources.

```json
{
  "version": 1,
  "git": {
    "sdl": {
      "git": "https://github.com/libsdl-org/SDL.git",
      "tag": "release-2.32.10",
      "commit": "9c1b6c1…"
    }
  }
}
```

- **Commit `forge.lock`.** It is what makes the build reproducible.
- `forge install` resolves new or changed dependencies; `forge install
  --update` re-resolves everything (use it to move to a newer tag after
  editing `forge.lua`).
- A moved upstream tag does *not* move your build — the commit is pinned.
- `forge build` never writes the lock, so a build needs no network beyond
  FetchContent's own fetch of the pinned commit.
- Local `path` dependencies are not locked (they are, by definition, whatever
  is on disk).

---

## vcpkg (manifest mode)

Forge writes `vcpkg.json` and hands CMake vcpkg's toolchain file, so vcpkg
installs the manifest's packages during configure — Forge never invokes vcpkg
itself.

```lua
dependencies = {
    direct = {},
    conan = {},
    vcpkg = {
        fmt = "fmt::fmt",
        spdlog = { target = "spdlog::spdlog", version = "1.12.0" }
    }
}
vcpkg_root = "external/vcpkg"     -- or $VCPKG_ROOT
vcpkg_baseline = "<commit>"       -- optional builtin-baseline
```

The declared `target` is what gets linked, because vcpkg cannot be queried for
targets without running it. vcpkg and Conan each need
`CMAKE_TOOLCHAIN_FILE`, so a project uses one or the other.

---

## pkg-config

System libraries that ship a `.pc` file need no wrapper: list the module names
and Forge resolves them.

```lua
dependencies = {
    direct = {},
    conan = {},
    pkgconfig = { "gtk+-3.0", "libcurl" }
}
```

Forge emits `find_package(PkgConfig REQUIRED)` and
`pkg_check_modules(<MODULE> REQUIRED IMPORTED_TARGET <module>)`, then links
`PkgConfig::<MODULE>`. Compile flags (include paths) come from the module, so a
system library's headers are available without extra configuration. A missing
module fails the configure with pkg-config's own message.

---

## Conan Packages

Forge has built-in support for [Conan](https://conan.io/), a powerful C/C++ package manager.

### Prerequisites

You must have `conan` installed on your system and configured. Forge will use the `conan` executable.

### Configuration

Add your Conan packages to the `dependencies.conan` table:

```lua
dependencies = {
    conan = {
        ["nlohmann_json"] = "3.11.2",
        ["spdlog"] = "1.12.0"
    }
}
```

### Usage

1. Run `forge build`. 
2. Forge will automatically generate a `conanfile.txt` in the `.config/` directory.
3. It then runs `conan install`.
4. Forge parses the output from Conan to identify the necessary `find_package` calls and `target_link_libraries` targets.
5. These are then injected into the generated `CMakeLists.txt`.

### How it works under the hood

Forge uses the `CMakeDeps` and `CMakeToolchain` generators from Conan. It points CMake to the generated toolchain file located in `build/build/Release/generators/conan_toolchain.cmake`.

---

## Which one should I use?

- **Use Direct Git Dependencies** when you want to build the dependency from source alongside your project, or when a Conan package is not available.
- **Use Conan Packages** for large libraries that take a long time to build, or when you want to use pre-compiled binaries for your specific platform/compiler.
