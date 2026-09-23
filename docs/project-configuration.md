# Project Configuration (forge.lua)

The `forge.lua` file is the heart of your project. It is a Lua script that returns a table containing all the settings Forge needs to build and manage your project.

## Basic Structure

A typical `forge.lua` looks like this:

```lua
return {
    project = {
        name = "my-project",
        type = "executable", -- or "library"
        standard = "20",     -- C++ standard (11, 14, 17, 20)
    },
    dependencies = {
        direct = {
            ["fmt"] = {
                git = "https://github.com/fmtlib/fmt.git",
                tag = "10.1.1"
            }
        },
        conan = {
            ["nlohmann_json"] = "3.11.2"
        }
    },
    scripts = {
        ["hello"] = "echo 'Hello from Forge!'"
    }
}
```

---

## Sections

### `project`
Contains metadata about your project.

- `name` (string): The name of your project. Used for the executable/library name.
- `type` (string): Either `"executable"` or `"library"`.
- `standard` (string): The C++ standard to use (e.g., `"17"`, `"20"`). Defaults to `"20"`.
- `cmake_policy_version` (string): (Optional) The minimum CMake policy version to use (e.g., `"3.5"`). Forge automatically detects your CMake version and applies a compatibility policy (defaulting to `"3.5"`) if you are using CMake 4.0 or newer. Use this field to override the default.
- `linkage` (string): (Optional) For libraries, specifies linkage.
- `version` (string): Project version, used by `project(... VERSION ...)`,
  `SOVERSION`, and `vcpkg.json`.
- `install_headers` (boolean): For libraries, if true, generates installation rules for the mirrored headers under `include/`. Defaults to `true` for libraries.
- `version_from_git` (boolean): (Optional) Derive the version from the newest
  Git tag instead of `version` — `2.5.0` on the tag, `2.5.0.3` three commits
  later, so it stays numeric for `project(... VERSION ...)` and `SOVERSION`. The
  declared `version` is kept when the repository has no version-like tag.
- `package_depends` (list or table): Runtime dependencies of the packages Forge
  builds, written to both `CPACK_DEBIAN_PACKAGE_DEPENDS` and
  `CPACK_RPM_PACKAGE_REQUIRES`. A plain list applies to both formats; the table
  form carries format-specific values (each format has its own syntax):

  ```lua
  package_depends = {
      "libstdc++6",                          -- both formats
      deb = { "libsdl2-2.0-0 (>= 2.0.20)" }, -- Debian syntax
      rpm = { "SDL2 >= 2.0.20" },            -- RPM syntax
  }
  ```

- `description` (string): (Optional) One-line summary, used as `CPACK_PACKAGE_DESCRIPTION_SUMMARY` when packaging.
- `contact` (string): (Optional) Maintainer contact (`CPACK_PACKAGE_CONTACT`). Required by the DEB and RPM package formats.

Libraries are header-installed automatically: `src/**/*.hpp` is copied to
`include/<name>/…` (sibling includes are copied verbatim; path-style includes are
rewritten to the installed form), and a `.cpp` without a hand-written header gets
one generated from its declarations, marked `// Auto-generated from …`. A
hand-written `.hpp` next to the `.cpp` always wins over the generated one.

### `dependencies`
Manages external libraries.

#### `dependencies.direct`
Dependencies that use CMake `FetchContent`, either fetched from Git or taken
from a local directory.
- `git` (string): The URL to the Git repository. Required unless `path` is set.
- `tag` (string): The branch, tag, or commit hash. Required unless `path` is set.
- `path` (string): A local directory to use as the dependency's source instead
  of Git. Relative paths resolve against the project directory. When set,
  `git` and `tag` are ignored.
- `target` (string): (Optional) The CMake target name to link against. The
  dependency **key** is the `FetchContent` name, so the two may differ.

#### `dependencies.conan`
Packages from the Conan package manager.
- Key: Package name.
- Value: Version string.

#### `dependencies.pkgconfig`
pkg-config modules, resolved with `pkg_check_modules` and linked through the
`PkgConfig::<MODULE>` imported targets.

```lua
dependencies = {
    direct = {},
    conan = {},
    pkgconfig = { "gtk+-3.0", "libcurl" }
}
```

Per-dependency CMake options:

```lua
direct = {
    sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.32.10",
            target = "SDL2::SDL2", options = { SDL_TEST = "OFF" } }
}
```

`options` are emitted as cache variables before the dependency is configured,
which is the only point where a fetched project's own `option()` calls can still
see them — useful to switch off a dependency's tests, examples or extra
backends.

#### `dependencies.vcpkg`
Packages resolved by [vcpkg](dependency-management.md#vcpkg-manifest-mode) in
manifest mode.
- Key: Package name.
- Value: The CMake target to link (`"fmt::fmt"`), or a table
  `{ target = "spdlog::spdlog", version = "1.12.0" }`.
- `vcpkg_root` (string): The vcpkg checkout; defaults to `$VCPKG_ROOT`, then
  `external/vcpkg`.
- `vcpkg_baseline` (string): `builtin-baseline` commit for `vcpkg.json`.
- `vcpkg_triplet` (string, top level): the vcpkg target triplet, passed as
  `VCPKG_TARGET_TRIPLET` so the toolchain builds dependencies for that platform
  (e.g. `x64-mingw-static`). Leave it empty to use vcpkg's default for the host.
- vcpkg and Conan both own `CMAKE_TOOLCHAIN_FILE`, so pick one per project.

### `targets`
Extra build targets beside the project's own one — a tools binary, a second
library, a plugin. The project's own target (`project.name`) is unchanged.

```lua
targets = {
    { name = "tools",  type = "executable", sources = { "tools" } },
    { name = "shared", type = "library", sources = { "lib/shared" }, linkage = "static" },
    { name = "plugin", type = "library", sources = { "plugins/a.cpp" }, install = true },
}
```

- `name` (string, required): the CMake target name. Letters, digits, `_` and `-`
  only; it must not repeat the project name or another target.
- `type`: `executable` (default) or `library`.
- `sources` (list): directories to glob for `.cpp` files, or individual files.
  Defaults to a directory named after the target (`tools/` for `tools`).
- `linkage`: `static` (default) or `shared`, for libraries.
- `install` (boolean): whether the target gets install rules. Defaults to true
  for libraries and false for executables.

Each target links the project's dependencies, and — like the test target — the
project's own extra libraries. A library's `sources` directory is added to its
public include path, so the project and its other targets can include its
headers. `forge run --bin <name>` runs a specific executable target.

### `resources`
Configures binary assets to be embedded into the executable.

```lua
resources = {
    files = {
        "assets/logo.png",
        "shaders/basic.vert"
    }
}
```

### `scripts`
Defines custom shell commands that can be run with `forge run <name>`.

```lua
scripts = {
    ["clean-assets"] = "rm -rf build/assets",
    ["pre-build"] = "echo 'Starting build...'"
}
```
*Special script names:*
- `pre-build`: Runs automatically before the build process starts.
- `post-build`: Runs automatically after the build process finishes.

### `features`
A flexible way to toggle project features or modules.

```lua
features = {
    ["graphics"] = {
        enabled = true,
        backend = "vulkan"
    },
    ["network"] = false -- Shortcut for enabled = false
}
```

### `testing`
`true` sets up a `test/` directory and a GoogleTest target (the default). The
table form picks another framework and enables benchmarks:

```lua
testing = {
    enabled = true,          -- default
    framework = "gtest",     -- gtest | catch2 | doctest
    benchmark = true         -- build a Google Benchmark target from bench/
}
```

- `gtest` — the dependency is injected into `forge.lua`; tests are discovered
  with `gtest_discover_tests`.
- `catch2` — resolved with `find_package(Catch2 3)`; tests are discovered with
  `catch_discover_tests`.
- `doctest` — fetched from GitHub; one ctest entry runs the binary.
- `benchmark` — adds a `<project>_bench` target linked against
  `benchmark::benchmark`, run with `forge bench`.

### `build`
Controls compiler/linker flags. Presets are applied directory-scoped, so they
affect your project and test targets but **not** fetched dependencies (e.g.
GoogleTest) or Conan-provided packages.

```lua
build = {
    presets = { "warnings", "concurrency" },   -- named bundles, applied in order
    cxx_flags = { "-fno-omit-frame-pointer" }, -- optional raw escape hatch
    link_flags = { "-rdynamic" },
    link_libraries = { "m" },
}
```

- `presets` (list of strings): Named flag bundles (see below). Unknown names
  produce a warning, so a typo is visible at configure time.
- `cxx_flags` / `link_flags` / `link_libraries` (lists): Raw flags prepended to
  the preset bundle for that category.
- `compile_definitions` (list): definitions emitted with
  `add_compile_definitions`, e.g. `{ "MY_FEATURE=1" }`.
- `compiler_launcher` (string): `ccache`/`sccache` for cached builds.
  Empty auto-detects on `PATH`; `"none"` disables it.
- `cxx_compiler` / `c_compiler` (string): compilers to configure with (path or
  name), e.g. `acpp` for a SYCL build.
- `toolchain_file` (string): an explicit CMake toolchain file. Cannot be
  combined with Conan or vcpkg dependencies (they all set the same variable).
- `cmake_prefix_path` (list): extra `CMAKE_PREFIX_PATH` entries, for SDK-style
  dependencies.
- `system_name` / `system_processor` (string): cross-compilation targets, e.g.
  `Linux` / `aarch64`.
- `generator` (string): CMake generator (`Ninja`, `"Unix Makefiles"`, …).
  Multi-config generators (Visual Studio, Xcode, Ninja Multi-Config) are
  detected and built with `--config` instead of `CMAKE_BUILD_TYPE`.
- `jobs` (number): default parallel job count; `--jobs` overrides it.
- `cache` (string): `"shared"` (default) reuses fetched git dependencies between
  projects and CI runs; `"off"` fetches them per project. The `FORGE_NO_CACHE`
  environment variable disables the cache too.
- `unity` (boolean): merge sources into one translation unit per target
  (`CMAKE_UNITY_BUILD`). Faster to compile, and a good way to catch missing
  includes — but duplicate internal symbols across files now collide.
- `pch` (string): a precompiled header applied to the project and test targets,
  e.g. `"src/pch.hpp"`. It is force-included, so it must be self-contained.
- `modules` (boolean): compile C++20 modules. Interface units (`src/*.cppm`,
  `src/*.ixx`) are put in a `CXX_MODULES` file set — CMake rejects them as plain
  sources — and module scanning is enabled (`CMAKE_CXX_SCAN_FOR_MODULES`). CMake
  only supports this with Ninja or Visual Studio 17.4+, so Forge selects
  **Ninja** when no generator is configured, and warns when the configured
  generator cannot scan.

`forge build` also writes **`CMakePresets.json`** with the same cache variables,
so IDEs (VS Code CMake Tools, CLion) and `cmake --preset forge` configure the
project exactly as Forge does. A hand-written preset file is left untouched.

The earlier spellings `compile_options` / `link_options` / `definitions` are
still accepted as aliases.

Available presets (GNU/Clang vs MSVC):

| Preset | GNU/Clang | MSVC |
|---|---|---|
| `production` | `-O3 -DNDEBUG` | `/O2` |
| `debug` | `-O0 -g` | `/Od /Zi` (+`/DEBUG` link) |
| `size` | `-Os` | `/O1` |
| `warnings` | `-Wall -Wextra -Wpedantic` | `/W4` |
| `warnings_as_errors` | `-Werror` | `/WX` |
| `concurrency` | `find_package(Threads)` + `Threads::Threads` | same |
| `simd` | `-march=native` | `/arch:AVX2` |
| `lto` | `-flto` (compile and link) | `/GL` + `/LTCG` |
| `asan` | `-fsanitize=address` | `/fsanitize=address` |
| `ubsan` / `tsan` / `sanitize` | the corresponding combination | — (unsupported) |
| `coverage` | `--coverage`, linked with `--coverage` | — (unsupported) |
| `fast_math` | `-ffast-math` | `/fp:fast` |
| `hardening` | `_FORTIFY_SOURCE=2`, `-fstack-protector-strong` | `/GS` |
| `no_exceptions` | `-fno-exceptions -fno-rtti` | `/EHs-c- /GR-` |

Preset flags are emitted behind compiler-ID guards
(`$<CXX_COMPILER_ID:GNU,Clang,AppleClang:…>` and `$<CXX_COMPILER_ID:MSVC:…>`),
so each toolchain only sees the flags it understands. The `sanitize`-family
presets also disable inlining (`-fno-omit-frame-pointer`) so stack traces stay
useful. Presets with no MSVC equivalent are silently inert there.

### `build`
Controls compiler/linker flags. Presets are applied directory-scoped, so they
affect your project and test targets but **not** fetched dependencies (e.g.
GoogleTest) or Conan-provided packages.

```lua
build = {
    presets = { "warnings", "concurrency" },   -- named bundles, applied in order
    cxx_flags = { "-fno-omit-frame-pointer" }, -- optional raw escape hatch
    link_flags = { "-rdynamic" },
    link_libraries = { "m" },
}
```

- `presets` (list of strings): Named flag bundles (see below). Unknown names
  produce a warning, so a typo is visible at configure time.
- `cxx_flags` / `link_flags` / `link_libraries` (lists): Raw flags prepended to
  the preset bundle for that category.

Available presets (GNU/Clang vs MSVC):

| Preset | GNU/Clang | MSVC |
|---|---|---|
| `production` | `-O3 -DNDEBUG` | `/O2` |
| `debug` | `-O0 -g` | `/Od /Zi` (+`/DEBUG` link) |
| `size` | `-Os` | `/O1` |
| `warnings` | `-Wall -Wextra -Wpedantic` | `/W4` |
| `warnings_as_errors` | `-Werror` | `/WX` |
| `concurrency` | `find_package(Threads)` + `Threads::Threads` | same |
| `simd` | `-march=native` | `/arch:AVX2` |
| `lto` | `-flto` (compile and link) | `/GL` + `/LTCG` |
| `asan` | `-fsanitize=address` | `/fsanitize=address` |
| `ubsan` / `tsan` / `sanitize` | the corresponding combination | — (unsupported) |
| `coverage` | `--coverage`, linked with `--coverage` | — (unsupported) |
| `fast_math` | `-ffast-math` | `/fp:fast` |
| `hardening` | `_FORTIFY_SOURCE=2`, `-fstack-protector-strong` | `/GS` |
| `no_exceptions` | `-fno-exceptions -fno-rtti` | `/EHs-c- /GR-` |

Preset flags are emitted behind compiler-ID guards
(`$<CXX_COMPILER_ID:GNU,Clang,AppleClang:…>` and `$<CXX_COMPILER_ID:MSVC:…>`),
so each toolchain only sees the flags it understands. The `sanitize`-family
presets also disable inlining (`-fno-omit-frame-pointer`) so stack traces stay
useful. Presets with no MSVC equivalent are silently inert there.

### `custom`
A free-form table of key/value pairs. Values are readable from Lua with
`forge.config.get()`, and keys that are valid CMake identifiers are also
emitted into the generated CMake as variables:

```lua
custom = {
    api_endpoint = "https://api.example.com",  -- Lua-only (not an identifier)
    WEBGPU_DIR   = "external/webgpu-sdk"       -- also: set(WEBGPU_DIR "...")
}
```

Keys must match `[A-Za-z_][A-Za-z0-9_]*` to be emitted; anything else stays
Lua-only. This is the declarative counterpart to a
[custom setup script](custom-setup.md).

Any top-level keys that aren't recognized by Forge are also added to the `custom` section.
