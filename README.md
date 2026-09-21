![Forge Logo](https://raw.githubusercontent.com/0xThurling/forge-cli/refs/heads/main/.branding/Gemini_Generated_Image_pizq48pizq48pizq%20(1).png)

# Forge: C++ Project Manager

`Forge` is a command-line tool designed to simplify the creation, building, and management of C++ projects. It provides a streamlined workflow for common development tasks using CMake and Lua.

## Features

*   **Project Scaffolding**: Quickly create new executable or library projects.
*   **Dependency Management**: Unified management of Git-based dependencies, local-path checkouts and Conan packages.
*   **Lua Configuration**: Flexible project configuration using `forge.lua`.
*   **Automated Builds**: Hands-off CMake generation and compilation.
*   **Resource Embedding**: Easily embed and access binary assets in your C++ code.
*   **Testing**: Integrated Google Test support.

## Installation

```bash
curl -sSL https://raw.githubusercontent.com/0xThurling/forge-cli/refs/heads/main/install.sh | bash
```

## Development

```bash
./compile.sh linux     # publish a standalone binary to ~/.local/bin
./dev.sh               # build + run every end-to-end scenario (no install)
./dev.sh path cache    # run only the named scenarios
./dev.sh --keep        # keep the scratch workspace for inspection
```

`dev.sh` drives the dev build directly (`dotnet bin/Release/net10.0/forge.dll`)
against a throwaway workspace, so the installed `forge` is never touched. It
covers local-path dependencies, missing paths, git dependencies, stale
build-cache recovery, and library header installation.

## Quick Start

1. **Create**: `forge create my_app`
2. **Build**: `forge build`
3. **Run**: `forge run`

## Configuration (`forge.lua`)

```lua
return {
  project = {
    name = "my_app",
    type = "executable",
    standard = "20"
  },
  dependencies = {
    direct = {
      fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1", target = "fmt::fmt" }
    }
  }
}
```

## Documentation

Comprehensive documentation on commands, architecture, and the Lua engine is available in our **[Project Wiki](https://tinyurl.com/forge-cli)**.

## ❤️ Support Forge

Forge is a free, open-source tool. If it saves you setup headaches and keeps your C++ workflow clean, consider sponsoring its development.

[![Sponsor on GitHub](https://img.shields.io/badge/Sponsor-%E2%9D%A4-red?logo=github)](https://github.com/sponsors/0xThurling)

## Contributing

Contributions are welcome! Please feel free to open issues or submit pull requests on [GitHub](https://github.com/0xThurling/forge-cli).

## License

This project is licensed under the MIT License.
