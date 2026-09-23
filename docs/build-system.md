# Build System & CMake

Forge is built on top of **CMake**, the industry standard for C++ build systems. However, instead of writing complex `CMakeLists.txt` files manually, you configure your project in `forge.lua`, and Forge handles the generation for you.

## The Generation Process

When you run `forge build`, the following happens:

1. **Configuration Loading**: Forge reads your `forge.lua`.
2. **Lua Scripting**: Any custom Lua scripts or build-time logic are executed.
3. **Dependency Resolution**: Conan packages are installed if necessary.
4. **Resource Generation**: Embedded resource files are generated.
5. **CMake Generation**: Forge generates a primary `CMakeLists.txt` in your project root and a detailed configuration file in `.config/cmake/CMakeLists.txt`.
6. **Compilation**: Forge invokes the `cmake` command to configure and build your project into the `build/` directory.

## Generated CMake Structure

Forge uses a modular approach to generate CMake files. The `.config/cmake/CMakeLists.txt` file is composed of several sections:

- **Standard Section**: Sets the C++ standard and basic project settings.
- **FetchContent Section**: Handles dependencies — Git repositories or local `path` directories.
- **Conan Section**: Handles `find_package` for Conan dependencies.
- **Project Target Section**: Defines the main executable or library target, including source files from `src/`.
- **Linking Section**: Handles linking all dependencies and libraries.
- **Testing Section**: Configures GoogleTest if enabled.
- **Custom Sections**: Any custom CMake snippets added via `forge.add_cmake()` in Lua.

## CMake Presets

Alongside the generated CMake files, every build writes `CMakePresets.json`
mirroring the configure settings (binary directory, generator, build type,
compilers, toolchain file, prefix path). That lets IDEs and
`cmake --preset forge` reproduce Forge's configuration without going through
the CLI. A `CMakePresets.json` that Forge did not generate is left untouched.

## The edit-build loop

```bash
forge watch                # rebuild whenever a source file changes
forge watch --command test # or re-run the tests
```

It scans `src`, `include`, `test` and `bench` (override with `--paths`), so a
save is followed by a build without a manual step. `--iterations 1` builds once
and exits, for scripts.

## Build speed

Three things happen before a single file is compiled, and all three are on by
default where they can be:

- **Parallel builds.** `forge build` passes `--parallel <jobs>` to CMake, with
  the job count from `--jobs`, then `build.jobs`, then every core.
- **A compiler launcher.** `ccache` or `sccache` is detected on `PATH` and used
  as `CMAKE_CXX_COMPILER_LAUNCHER`; set `build.compiler_launcher = "none"` to
  opt out, or name a different one.
- **Configured-only-when-needed.** The generated files are written on every
  build, but CMake is only re-run when they (or the cache variables) changed.

Unity builds and precompiled headers cut compile time further — see
[Modern build features](#modern-build-features). For tracking what a change
costs, `forge bench --save`/`--compare` records baselines and reports deltas.

## Several targets in one project

A project can build more than one target — an application plus a tools binary,
or a library its application links — through the `targets` table:

```lua
targets = {
    { name = "tools",  sources = { "tools" } },
    { name = "shared", type = "library", sources = { "lib/shared" } },
}
```

Each target globs its own directory, links the project's dependencies and its
extra libraries, and (for libraries) gets install rules. `forge run --bin tools`
runs a chosen executable. The project's own target keeps its name, version and
export rules, so consumers see no difference.

## Installing and consuming a library

`forge install --prefix <dir>` installs the project into a CMake-style tree:

```text
<dir>/lib/lib<name>.a          # or .so, with SOVERSION from project.version
<dir>/include/…                # the mirrored headers
<dir>/lib/cmake/<name>/<name>Config.cmake
```

Another project consumes it with plain CMake — no Forge involved:

```cmake
find_package(mylib CONFIG REQUIRED)
target_link_libraries(app PRIVATE mylib::mylib)
```

`project.version` drives the library version and its `SOVERSION`. A consumer
that also uses Forge does not need the installed tree: a `git` or `path`
dependency builds it from source instead, and its headers are used in place.

## Modern build features

Three `build` toggles cover the usual "why is my build slow" answers:

```lua
build = {
    unity = true,             -- one translation unit per target
    pch = "src/pch.hpp",      -- force-included precompiled header
    modules = true,           -- C++20 module scanning (needs Ninja)
}
```

`unity` is the cheapest win for small projects and the strictest check that
every file includes what it uses. `pch` pays off when a heavy header (a
standard library umbrella, a framework header) is included nearly everywhere.
`modules` compiles C++20 module interface units (`src/*.cppm`, `src/*.ixx`) —
they go into a `CXX_MODULES` file set, since CMake rejects them as ordinary
sources — and changes the generator when needed, because CMake can only scan for
modules with Ninja or Visual Studio 17.4+.

## Packaging

A versioned project gets a CPack section in the generated file
(`packaging`, priority 55) and can be packaged with `forge publish`:

```bash
forge publish                        # dist/<name>-<version>-<system>.tar.gz
forge publish --format ZIP           # or TGZ, DEB, RPM, …
```

Libraries install themselves (library, headers and CMake package file);
executables are installed to `bin/`. Add `project.description` and
`project.contact` for nicer packages — DEB and RPM require the contact.

## Extending the generated CMake

Two Lua hooks cover setup the declarative configuration cannot express:
`forge.add_cmake` injects raw text at a fixed point (pre/post target), and
`forge.add_section` registers a *named* section at a chosen anchor
(`"after:project_target"`, `"before:testing"`, …). See the
[Lua reference](lua-reference.md#forgeadd_sectionname-position-content).

## Continuous integration

`forge ci` writes a workflow for the project instead of asking you to
transcribe your configuration into YAML by hand:

```bash
forge ci                 # .github/workflows/forge.yml
forge ci --provider gitlab
```

The matrix, the toolchain install, the dependency-channel setup, the test and
lint steps and the packaging step all follow from `forge.lua` and the project's
own files. Regenerate with `--force` after changing the configuration, or use
`forge ci --check` to fail when the workflow is stale.

## Quality tooling

`forge build` produces a compile database, which is what the other tools need:
`forge lint` runs clang-tidy over it, and `forge format` runs clang-format over
the same source set (`src`, `test`, `bench`). Both write a sensible config file
on first use (`.clang-tidy`, `.clang-format`) that you are expected to edit.

## Customizing CMake

While Forge automates most things, you can still inject custom CMake code using the Lua API:

```lua
-- In forge.lua or a build script
forge.add_cmake([[
    if(MSVC)
        add_compile_options(/W4 /WX)
    else()
        add_compile_options(-Wall -Wextra -Werror)
    endif()
]])
```

## Automatic CMake Compatibility

To ensure compatibility with older dependencies (like `raylib`) when using modern CMake versions, Forge includes an automatic compatibility layer:

- **Detection**: Forge automatically detects your installed CMake version.
- **Auto-Policy**: If CMake 4.0 or newer is detected, Forge automatically applies `CMAKE_POLICY_VERSION_MINIMUM=3.5`. This prevents errors caused by the removal of support for older CMake versions in newer releases.
- **Manual Override**: You can override this behavior by setting `cmake_policy_version` in the `project` section of your `forge.lua`.

## LSP Support

Forge automatically generates a `compile_commands.json` file and creates a symbolic link in your project root. This ensures that Language Servers (like `clangd` or the C++ extension in VS Code) have all the information they need for features like autocomplete, go-to-definition, and error highlighting, even with complex dependencies.

## Build Artifacts

- **Executables**: Found in `build/`.
- **Libraries**: Found in `build/` (e.g., `libMyLib.a` or `MyLib.lib`).
- **Temporary Files**: Stored in `build/` and `.config/`.

### Moved or renamed projects

CMake records the source directory it was configured for, and refuses to
configure a cache that belongs to a different one. If you move, rename or
re-clone a project (or copy a `build/` directory between machines), Forge
detects the mismatch on the next `forge build`, regenerates the cache — keeping
`build/_deps/*-src` so fetched dependency sources are not downloaded again —
and continues. No manual `rm -rf build` needed.
