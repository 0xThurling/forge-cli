# Welcome to Forge

**Forge** is a modern, Lua-scriptable C++ project manager CLI tool designed to simplify the lifecycle of C++ development. It provides project scaffolding, dependency management (Git & Conan), CMake integration, and powerful build automation.

## Key Features

- **🚀 Quick Scaffolding**: Create new projects (`forge create`), adopt an existing directory (`forge init`), and add classes, structs, headers or sources with a single command.
- **📜 Lua Configuration**: Use `forge.lua` for a clean, human-readable project configuration.
- **📦 Dependency Management**: Git, local `path` checkouts, Conan, vcpkg and pkg-config, with `forge.lock` pinning, a shared dependency cache, `forge outdated`/`forge upgrade`, and `forge vendor` for offline builds.
- **⚙️ CMake Integration**: Automatically generates and manages `CMakeLists.txt`, with several targets per project, parallel builds, ccache/sccache, toolchain and generator selection, unity builds, precompiled headers and C++20 module scanning.
- **🧪 Testing and Benchmarks**: GoogleTest, Catch2 or doctest, plus Google Benchmark (`forge bench`).
- **🧹 Quality Tooling**: `forge format` and `forge lint` (clang-format/clang-tidy), and `forge doctor --fix`.
- **🧰 Toolchain Setup**: `forge setup` reports the tools Forge uses (and installs what is missing), `forge doctor` checks the environment, and `forge ci` writes the same steps into the workflow it generates.
- **📦 Packaging**: `forge publish` builds a CPack archive (TGZ, ZIP, DEB, RPM) from a versioned project.
- **🤖 CI Generation**: `forge ci` writes a GitHub Actions workflow or GitLab pipeline that matches the configuration.
- **🗂️ Workspaces**: Build and test a directory of sibling projects in dependency order (`forge workspace`).
- **💎 Resource Embedding**: Easily embed binary assets (images, shaders, etc.) directly into your executables.
- **🛠️ Extensible Build Automation**: Custom Lua scripts with a rich API, `cmakeOptions`, and named CMake sections at a chosen anchor (`forge.add_section`).

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

Then pin and build:

```bash
forge install    # resolve git dependencies into forge.lock
forge build      # fetch, build, link
```

## Where to go next

| Page | What it covers |
|---|---|
| [Project Configuration](project-configuration.md) | every `forge.lua` field |
| [Dependency Management](dependency-management.md) | git, `path`, Conan, vcpkg, pkg-config, lockfile |
| [Build System](build-system.md) | generated CMake, presets, build toggles, packaging, quality tooling |
| [Workspaces](workspaces.md) | several sibling projects, built in dependency order |
| [Project Scaffolding](project-scaffolding.md) | `forge create`, `forge new` |
| [Custom Setup Scripts](custom-setup.md) | `.config/forge/build/*.lua`, `cmakeOptions` |
| [CLI Reference](cli-reference.md) | every command and option |
| [Lua Reference](lua-reference.md) | the `forge.*` API, including `forge.add_section` |

