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

### `custom`
A free-form table for any additional configuration you want to access via the Lua API using `forge.config.get()`.

```lua
custom = {
    api_endpoint = "https://api.example.com"
}
```
Any top-level keys that aren't recognized by Forge are also added to the `custom` section.
