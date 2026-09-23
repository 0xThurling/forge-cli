# Lua API Reference

Forge provides a powerful Lua API that allows you to script your build process, manage external resources, and interact with the project configuration. These functions are available under the global `forge` table.

## Logging

### `forge.log.info(message)`
Logs an informational message to the console.
- `message`: The string to log.

### `forge.log.warn(message)`
Logs a warning message to the console.

### `forge.log.error(message)`
Logs an error message to the console.

---

## Configuration

### `forge.config.get(key)`
Retrieves a value from the `custom` section of `forge.lua`.
- `key`: The key to retrieve.
- **Returns**: The value as a string, or `nil` if not found.

### `forge.config.set(key, value)`
Writes a configuration value **to `forge.lua`** (dotted keys reach nested
sections, e.g. `project.name`, `dependencies.fmt.tag`, `conan.fmt`; anything
else lands in `custom`).
- `key`: The key to set.
- `value`: The value to set.

The change is persistent, but the build that is currently running keeps the
configuration it loaded at the start — the new value is used from the next
`forge build`. A `custom` key that is a valid CMake identifier is emitted as a
CMake variable by the next build.

### `forge.config.has_feature(name)`
Checks if a feature is enabled in the `features` section.
- `name`: The name of the feature.
- **Returns**: Boolean.

### `forge.config.get_feature_option(feature, option, default)`
Retrieves an option from a specific feature.
- `feature`: The feature name.
- `option`: The option key.
- `default`: The default value to return if not found.

---

## External Resources & Git

### `forge.pull_repo(url, tag?)`
Clones a Git repository into the `external/` directory.
- `url`: The Git repository URL.
- `tag`: (Optional) The branch, tag, or commit hash to clone.

### `forge.fetch(url, output_dir?)`
Downloads a file (usually a zip), extracts it, and returns the path to the extracted directory.
- `url`: The URL to download from.
- `output_dir`: (Optional) The directory to extract to. Defaults to `external/<name>`.
- **Returns**: The path to the extracted directory.

### `forge.download(url, output, options?, progress_callback?)`
Downloads a file to a specific location.
- `url`: The URL to download.
- `output`: The destination file path.
- `options`: (Optional) A table of options (e.g., `{ timeout = 300 }`).
- `progress_callback`: (Optional) A function receiving `(bytes_downloaded, total_bytes)`.
- **Returns**: The number of bytes downloaded.

### `forge.extract(archive_path, output_dir, strip_components?)`
Extracts a ZIP or TAR archive.
- `archive_path`: Path to the archive.
- `output_dir`: Destination directory.
- `strip_components`: (Optional) Number of leading components to strip (only for TAR archives).

---

## Running programs and files

These helpers exist because the embedded Lua runtime has no `io.popen`: a script
can run a command with `os.execute`, but cannot read what it printed. They are
relative to the project root, and create the directories they need.

### `forge.exec(command)`
Runs a command through the shell and captures its output.
- `command`: The command line to run.
- Returns: the exit code, then the combined stdout+stderr.

```lua
local code, output = forge.exec("glslangValidator -V shader.vert")
if code ~= 0 then error("shader compilation failed:\n" .. output) end
```

### `forge.read_file(path)`
Returns the file's contents, or `nil` when it does not exist.

### `forge.write_file(path, contents)`
Writes a file, creating parent directories. Returns the path.

### `forge.copy_file(source, destination)`
Copies a file, creating parent directories. Returns the destination.

### `forge.mkdir(path)`
Creates a directory and its parents. Returns the path.

### `forge.template(source, destination, values)`
Reads a template and replaces `@KEY@` placeholders with the values table.
Returns the destination.

```lua
forge.template("templates/version.h.in", "generated/version.h", {
  VERSION = forge.git.describe() or "unknown",
})
```

### `forge.git.describe()` / `rev()` / `tag()` / `branch()` / `dirty()`
Read the project's Git state: the newest tag with distance and dirty flag, the
abbreviated commit, the exact tag at HEAD, the branch, and whether the working
tree has uncommitted changes. Each returns `nil` (or `false` for `dirty`) when
git is missing or the directory is not a repository, so a script can fall back
with `or "unknown"`.

## Build System Integration

### `build` section
Declarative compiler/linker flags read from `forge.lua` (presets plus raw
flags). Applied directory-scoped so dependencies are unaffected. Each preset
emits a GNU/Clang flag (guarded by compiler ID) and, where one exists, an MSVC
equivalent — see
[Project Configuration](project-configuration.md#build) for the full table.

```lua
build = {
    presets = { "warnings", "concurrency" },
    cxx_flags = { "-fno-omit-frame-pointer" },
    link_flags = { "-rdynamic" },
    link_libraries = { "m" },
}
```

CLI overrides for a single invocation:
- `forge build --preset asan,ubsan` replaces the configured presets.
- `forge build --no-config-presets` ignores `build.presets` entirely.
- `forge build --release` / `--debug` force `CMAKE_BUILD_TYPE`.
- `forge test` forwards the same flag options to the build step.

### Environment tables
Values describing the machine, for setup scripts that have to branch:

| Value | Contents |
|---|---|
| `forge.current_working_dir` | the project root (scripts always run from there) |
| `forge.os.current` | `forge.os.windows` / `forge.os.macos` / `forge.os.linux` — one of them, matching the running platform |
| `forge.distro.my_distro` | the detected Linux distribution: `nixos`, `fedora`, `manjaro`, `arch`, `ubuntu`, `debian`, `redhat` or `unknown` |
| `forge.package_manager.*` | `winget`, `chocolatey`, `brew`, `pacman`, `aptget`, `no_pass` |

```lua
-- .config/forge/build/deps.lua
local pm = forge.package_manager
if forge.os.current == forge.os.linux and forge.distro.my_distro == forge.distro.arch then
  forge.get_packages(pm.no_pass, pm.pacman, { "vulkan-headers", "vulkan-icd-loader" })
end
return {}
```

### `forge.add_cmake(snippet, phase?)`
Adds a custom CMake snippet directly into the generated `CMakeLists.txt`.
- `snippet`: The CMake code to inject. `${PROJECT_NAME}` is replaced with the
  project name.
- `phase`: (Optional) `"pre"` injects the snippet *before* the project target is
  created — for variables, `find_package`, `add_subdirectory` and toolchain
  setup. Anything else (the default) injects it *after* the target and its link
  line, for `set_target_properties`, install rules and extra
  `target_link_libraries`.

Build scripts can also describe their contribution declaratively with the
`cmakeOptions` table they return — see
[Custom Setup Scripts](custom-setup.md).

### `cmakeOptions.cacheVariables`
Like `variables`, but emitted as
`set(NAME "value" CACHE STRING "" FORCE)`. Use it for settings a toolchain file
or a dependency's `option()` reads — a plain `set()` happens too late for
those.

```lua
return { cmakeOptions = { cacheVariables = { WEBGPU_BACKEND = "d3d12" } } }
```

### `forge.add_section(name, position, content)`
Registers a named CMake section, placed relative to the built-in ones. Unlike
`forge.add_cmake`, the section keeps its identity: it is emitted at a chosen
point in the generated file and a later registration with the same name
replaces it.

```lua
forge.add_section("codegen", "after:project_target", [[
target_compile_definitions(${PROJECT_NAME} PRIVATE FROM_CODEGEN=1)
]])
```

- `name`: Identifier (letters, digits, `_`, `-`). It must not shadow a built-in
  section; it is shown as `# --- <name> (from Lua) ---`.
- `position`: Where to put it. `"before:<section>"`, `"after:<section>"`,
  `"first"`, `"last"` (the default when omitted), or a raw priority number.
- `content`: The CMake to emit. `${PROJECT_NAME}` is replaced with the project
  name.

Built-in section names, in emission order — these are the valid anchors:

| Anchor | Priority | Contains |
|---|---|---|
| `standard` | 1 | C++ standard and language extensions |
| `features` | 2 | `features` options |
| `conan` | 5 | Conan toolchain and dependencies |
| `vcpkg` | 6 | vcpkg toolchain and dependencies |
| `pkgconfig` | 7 | `pkg_check_modules` |
| `fetchcontent` | 10 | git and `path` dependencies |
| `flags` | 20 | flag presets, unity/module toggles |
| `lua-options` | 25 | `cmakeOptions` and pre-phase snippets |
| `project_target` | 30 | the target, its install rules and PCH |
| `linking` | 40 | the link line |
| `custom` | 45 | post-phase `add_cmake` snippets |
| `testing` | 50 | the test (and benchmark) targets |
| `packaging` | 55 | CPack configuration |

An unknown anchor or position is reported and the section goes last, so a typo
never breaks the build silently.

### `forge.get_packages(password, manager, packages)`
Installs system packages using a package manager (e.g., `apt`, `pacman`).
- `password`: The sudo password (use `"nopass"` if not required).
- `manager`: The package manager name.
- `packages`: A Lua table (list) of package names.
