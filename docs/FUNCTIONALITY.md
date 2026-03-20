# Forge Functionality Documentation

## Overview

Forge provides a comprehensive set of CLI commands for C++ project management. This document details all available functionality.

## Commands Reference

### Project Management

#### `forge create <project_name>`

Creates a new C++ project with standard directory structure.

**Arguments:**
- `project_name` (required) - Name of the project to create

**Options:**
- `--type <executable|library>` - Project type (default: executable)

**Directory Structure Created:**
```
project_name/
├── src/                    # Source files
├── external/               # External dependencies
├── assets/                 # Resource files
├── .config/
│   └── forge/
│       ├── commands/       # Custom commands
│       ├── build/          # Build scripts
│       ├── templates/      # Code templates
│       └── definitions/    # Lua definitions
├── .gitignore
└── package.toml
```

**Example:**
```bash
forge create my_game --type executable
```

---

#### `forge build`

Generates CMakeLists.txt and builds the project.

**Options:**
- `--verbose` - Show verbose CMake output
- `--standard <11|14|17|20>` - C++ standard (default: 20)

**Process:**
1. Runs pre-build script (if defined)
2. Installs Conan dependencies
3. Generates resource files
4. Generates CMakeLists.txt
5. Runs CMake configure
6. Runs CMake build
7. Creates compile_commands.json symlink
8. Runs post-build script (if defined)

**Example:**
```bash
forge build --verbose
forge build --standard 17
```

---

#### `forge run [script_name]`

Runs the project or a custom script.

**Arguments:**
- `script_name` (optional) - Name of script from package.toml

**Behavior:**
- Without arguments: Builds and runs the executable
- With script_name: Runs the corresponding script from package.toml

**Example:**
```bash
forge run                      # Build and run
forge run pre-build            # Run pre-build script
```

---

#### `forge test`

Builds and runs tests using Google Test.

**Process:**
1. Creates test directory if needed
2. Creates test/main.cpp if needed
3. Adds googletest dependency to package.toml (if missing)
4. Builds project
5. Runs tests via Google Test

**Example:**
```bash
forge test
```

---

#### `forge clean`

Removes the build directory.

**Example:**
```bash
forge clean
```

---

### Code Generation

#### `forge new class <ClassName>`

Generates a new C++ class with header and source files.

**Arguments:**
- `ClassName` - Name of the class

**Output:**
- `src/ClassName.h` - Header file with include guards
- `src/ClassName.cpp` - Source file with constructor/destructor

**Example:**
```bash
forge new class Player
```

---

#### `forge new struct <StructName>`

Generates a new C++ struct.

**Arguments:**
- `StructName` - Name of the struct

**Example:**
```bash
forge new struct Vector3
```

---

#### `forge new header <FileName>`

Generates a header file with include guards.

**Arguments:**
- `FileName` - Name of the header file (without extension)

**Example:**
```bash
forge new header my_header
# Creates src/my_header.h
```

---

#### `forge new source <FileName>`

Generates a header and source file pair.

**Arguments:**
- `FileName` - Name of the file (without extension)

**Example:**
```bash
forge new source utils
# Creates src/utils.h and src/utils.cpp
```

---

### Resource Management

#### `forge embed <file_path>`

Registers a resource file to be embedded in the executable.

**Arguments:**
- `file_path` - Path to the resource file

**Process:**
1. Validates file exists
2. Adds relative path to package.toml
3. Resource is embedded during next build

**Example:**
```bash
forge embed assets/icon.png
forge embed assets/shader.glsl
```

---

### Project Information

#### `forge project info`

Displays project configuration summary.

**Shows:**
- Project name
- Project type
- C++ standard
- Dependencies (Git + Conan)
- Registered resources

---

#### `forge project tree`

Displays project file structure in tree format.

**Example output:**
```
my_project/
├── src/
│   ├── main.cpp
│   ├── Player.h
│   └── Player.cpp
├── assets/
│   └── icon.png
├── external/
└── package.toml
```

---

#### `forge project stats`

Displays project statistics.

**Shows:**
- Total files
- Source files
- Header files
- Test files
- Resource files

---

#### `forge project dependencies`

Lists all project dependencies.

**Shows:**
- Git-based dependencies (name, git URL, tag)
- Conan dependencies (name, version)

---

#### `forge project scripts`

Lists available scripts from package.toml.

**Shows:**
- Script names and their commands

---

### Package Management

#### `forge install`

Installs Conan dependencies defined in package.toml.

**Process:**
1. Generates conanfile.txt from package.toml
2. Runs `conan install`
3. Parses output for CMake targets
4. Links dependencies during build

**Example:**
```bash
forge install
# Or implicitly via:
forge build
```

---

## package.toml Configuration

### Project Section

```toml
[project]
name = "my_project"           # Project name
type = "executable"           # executable or library
install_headers = false      # Install headers (libraries only)
```

### Dependencies Section

Git-based dependencies using FetchContent:

```toml
[dependencies]
# Format:
# name = { git = "url", tag = "version", target = "cmake_target" }

sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
```

### Conan Dependencies Section

Conan package manager integration:

```toml
[conan-dependencies]
# Format:
# package_name = "version"

fmt = "10.2.1"
spdlog = "1.12.0"
```

### Resources Section

Files to embed in executable:

```toml
[resources]
files = [
    "assets/icon.png",
    "assets/shader.glsl",
    "assets/config.json"
]
```

### Scripts Section

Custom scripts that can be run via `forge run`:

```toml
[scripts]
pre-build = "echo 'Building project...'"
post-build = "echo 'Build complete!'"
compile-shaders = "python3 scripts/compile_shaders.py"
```

---

## Embedded Resources API

After running `forge embed` and rebuilding, resources are accessible via generated code:

```cpp
#include "embedded_resources.h"

// Access embedded resource
const auto& resource = Embedded::get("icon.png");
const unsigned char* data = resource.data;
size_t size = resource.size;
```

---

## Lua Scripting

### Build Scripts

Place `.lua` files in `.config/forge/build/` to run during build:

```lua
-- example build script
forge.log.info("Running custom build script")

-- Clone external repository
forge.pull_repo("https://github.com/example/library.git")

-- Install system packages
forge.get_packages("nopass", forge.package_manager.brew, {"sdl2"})
```

### Environment Variables

Lua scripts have access to environment information:

```lua
-- Current working directory
print(forge.current_working_dir)

-- Operating system
print(forge.os.current)  -- "linux", "macos", "windows"

-- Distribution (Linux)
print(forge.distro.my_distro)  -- "ubuntu", "arch", etc.

-- Package managers
forge.package_manager.brew
forge.package_manager.aptget
forge.package_manager.pacman
```

---

## Testing

### Test Structure

Tests should be placed in the `test/` directory:

```
test/
└── main.cpp
```

### Running Tests

```bash
forge test
```

This will:
1. Ensure Google Test is installed
2. Build the test executable
3. Run all tests

### Writing Tests

```cpp
#include <gtest/gtest.h>

TEST(TestSuite, TestName) {
    EXPECT_EQ(1 + 1, 2);
}

int main(int argc, char** argv) {
    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
```

---

## Editor Integration

### LSP Support

Forge automatically generates `compile_commands.json` for Language Server Protocol support.

After building, a symlink is created in the project root pointing to the build directory's compile_commands.json.

**Supported editors:**
- VS Code (with C++ extensions)
- Neovim (with clangd)
- Emacs (with eglot)
- CLion (automatic)

---

## Error Handling

Forge provides user-friendly error messages for common issues:

| Error | Cause | Solution |
|-------|-------|----------|
| `cmake not found` | CMake not installed | Install CMake |
| `package.toml not found` | Not in project root | Run from project directory |
| `conan not found` | Conan not installed | Install Conan |
| `Script not found` | Invalid script name | Check package.toml scripts |

---

## Best Practices

1. **Always use relative paths** for resources in package.toml
2. **Define pre/post-build scripts** for repetitive tasks
3. **Use Conan dependencies** for large libraries (SDL, Boost, etc.)
4. **Use Git dependencies** for smaller, project-specific libraries
5. **Embed static assets** (shaders, configs) rather than loading at runtime
6. **Keep compile_commands.json** for editor LSP support
