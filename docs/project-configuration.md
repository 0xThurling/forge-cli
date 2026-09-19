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
- `install_headers` (boolean): For libraries, if true, generates installation rules for headers in `src/`. Defaults to `true` for libraries.

### `dependencies`
Manages external libraries.

#### `dependencies.direct`
Git-based dependencies that use CMake `FetchContent`.
- `git` (string): The URL to the Git repository.
- `tag` (string): The branch, tag, or commit hash.
- `target` (string): (Optional) The CMake target name to link against.

#### `dependencies.conan`
Packages from the Conan package manager.
- Key: Package name.
- Value: Version string.

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

### `testing` (boolean)
If set to `true`, Forge will automatically configure GoogleTest and set up a `test/` directory.

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
A free-form table for any additional configuration you want to access via the Lua API using `forge.config.get()`.

```lua
custom = {
    api_endpoint = "https://api.example.com"
}
```
Any top-level keys that aren't recognized by Forge are also added to the `custom` section.
