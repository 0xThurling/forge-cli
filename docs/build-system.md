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
- **FetchContent Section**: Handles Git dependencies.
- **Conan Section**: Handles `find_package` for Conan dependencies.
- **Project Target Section**: Defines the main executable or library target, including source files from `src/`.
- **Linking Section**: Handles linking all dependencies and libraries.
- **Testing Section**: Configures GoogleTest if enabled.
- **Custom Sections**: Any custom CMake snippets added via `forge.add_cmake()` in Lua.

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

## LSP Support

Forge automatically generates a `compile_commands.json` file and creates a symbolic link in your project root. This ensures that Language Servers (like `clangd` or the C++ extension in VS Code) have all the information they need for features like autocomplete, go-to-definition, and error highlighting, even with complex dependencies.

## Build Artifacts

- **Executables**: Found in `build/`.
- **Libraries**: Found in `build/` (e.g., `libMyLib.a` or `MyLib.lib`).
- **Temporary Files**: Stored in `build/` and `.config/`.
