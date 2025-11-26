# Forge: C++ Project Manager

`Forge` (C++ Project Manager) is a command-line tool designed to simplify the creation, building, and management of C++ projects using CMake. It provides a streamlined workflow for common development tasks, including project scaffolding, dependency management, and automated code generation.

## Features

*   **Project Creation:** Quickly scaffold new C++ projects with a standard directory structure, including `src/` and `assets/` directories.
*   **Dependency Management:** Declare Git-based dependencies in a `package.toml` file, which `forge` automatically fetches and integrates into your CMake build.
*   **Automated Builds:** Generates `CMakeLists.txt` based on your `package.toml` and handles the entire CMake build process.
*   **Resource Management:** Embed assets (images, shaders, etc.) directly into your executable and access them through a simple, generated API.
*   **Code Generation:** Generate boilerplate for new C++ classes, structs, blank headers, or source file pairs.
*   **All-in-One Testing:** A single command to create the test harness (if needed), build, and run your tests using Google Test.
*   **LSP Support:** Automatically generates `compile_commands.json` for improved Language Server Protocol (LSP) support in editors.

## Installation

### Using the install script (macOS and Linux)

You can install `forge` by running the following command in your terminal:

```bash
curl -sSL https://raw.githubusercontent.com/0xThurling/forge/main/install.sh | bash
```

This will download and run the `install.sh` script, which will install the `forge` binary in `/usr/local/bin`.

### Manual Installation

1.  Download the latest binary for your platform from the [releases page](https://github.com/0xThurling/forge/releases).
2.  Make the binary executable:
    ```bash
    chmod +x forge
    ```
3.  Move the binary to a directory in your `PATH`. For example:
    ```bash
    # For macOS and Linux
    sudo mv forge /usr/local/bin/
    ```

### Supported Platforms

*   macOS (x64, arm64)
*   Linux (x64)

## CLI Commands

### `forge create <project_name>`

Creates a new C++ project with a standard directory structure.

### `forge build`

Generates `CMakeLists.txt` and builds the project.

### `forge run [script_name]`

If a `script_name` is provided, it will run the corresponding script from the `package.toml` file. If no scripts are defined, it will build and run the project.

### `forge project info`

Displays a summary of the project's configuration.

### `forge project tree`

Displays a tree-like structure of the project's files and directories.

### `forge project stats`

Displays some statistics about the project.

### `forge project dependencies`

Lists the project's dependencies and their versions.

### `forge project scripts`

Lists all the scripts in the project.

### `forge test`

Builds and runs tests. It will automatically set up Google Test if not present.

### `forge clean`

Removes the `build/` directory.

### `forge embed <file_path>`

Embeds a resource file into the executable.

### `forge new <entity> <name>`

Generates boilerplate code.
*   `class <ClassName>`: Creates a class.
*   `struct <StructName>`: Creates a struct.
*   `header <FileName>`: Creates a header file.
*   `source <FileName>`: Creates a header and source file.

## `package.toml` Configuration

### Example `package.toml`

```toml
[project]
name = "my_project"
type = "executable"

[dependencies]
# Example:
# sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target="SDL2::SDL2" }

[resources]
files = [
    # "assets/icon.png",
    # "assets/shader.glsl"
]
```

### `[resources]` Section

*   `files`: A list of file paths for assets you want to embed in your project via the `forge embed` command.

### `[scripts]` Section

*   You can define custom scripts in your `package.toml` file that can be run with `forge run <script_name>`.
*   `pre-build`: This script will be run before the build process starts.
*   `post-build`: This script will be run after the build process has finished successfully.

```toml
[scripts]
pre-build = "echo pre-build"
post-build = "echo post-build"
```

### Conan Package Management (Pre-release)

`forge` offers experimental support for [Conan](https://conan.io/), a C++ package manager, allowing you to integrate third-party libraries seamlessly.

To add Conan packages, create a `[conan-dependencies]` section in your `package.toml` file. Specify each package and its version in the format `package_name = "version"`.

For example:

```toml
[conan-dependencies]
fmt = "10.2.1"
```

After adding your dependencies, you can run `forge install`, `forge build`, or `forge run`. These commands will automatically detect the changes in `package.toml`, fetch the specified Conan packages, and configure your CMake project to use them.

## Contributing

Contributions are welcome! Please feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License.
