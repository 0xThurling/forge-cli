# Welcome to Forge

**Forge** is a modern, Lua-scriptable C++ project manager CLI tool designed to simplify the lifecycle of C++ development. It provides project scaffolding, dependency management (Git & Conan), CMake integration, and powerful build automation.

## Key Features

- **🚀 Quick Scaffolding**: Create new projects, classes, structs, and headers with a single command.
- **📜 Lua Configuration**: Use `forge.lua` for a clean, human-readable project configuration.
- **📦 Dependency Management**: Seamlessly integrate Git repositories (via CMake FetchContent) and Conan packages.
- **💎 Resource Embedding**: Easily embed binary assets (images, shaders, etc.) directly into your executables.
- **🛠️ Scriptable Build Automation**: Extend your build process with custom Lua scripts and a rich API.
- **⚙️ CMake Integration**: Automatically generates and manages `CMakeLists.txt` based on your configuration.

## Getting Started

### Installation

The easiest way to install Forge is using our installation script:

```bash
curl -sSL https://raw.githubusercontent.com/0xThurling/forge-cli/refs/heads/main/install.sh | bash
```

### Creating a New Project

To start a new C++ project:

```bash
forge create my-awesome-app --type executable
cd my-awesome-app
```

### Building and Running

Forge handles the CMake generation and build process for you:

```bash
forge build
forge run
```

### Adding a Dependency

Add a Git dependency to your `forge.lua`:

```lua
return {
    project = {
        name = "my-app",
        type = "executable"
    },
    dependencies = {
        direct = {
            ["fmt"] = {
                git = "https://github.com/fmtlib/fmt.git",
                tag = "10.1.1"
            }
        }
    }
}
```

Then download and build:

```bash
forge download
forge build
```

