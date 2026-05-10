# Dependency Management

Forge supports two primary ways to manage your project's dependencies: **Direct Git Dependencies** (via CMake FetchContent) and **Conan Packages**.

## Direct Git Dependencies

Direct dependencies are managed by Forge using CMake's `FetchContent` module. This is ideal for libraries that are available on GitHub and can be built alongside your project.

### Configuration

Add your dependencies to the `dependencies.direct` table in `forge.lua`:

```lua
dependencies = {
    direct = {
        ["fmt"] = {
            git = "https://github.com/fmtlib/fmt.git",
            tag = "10.1.1" -- Can be a tag, branch, or commit hash
        },
        ["glm"] = {
            git = "https://github.com/g-truc/glm.git",
            tag = "0.9.9.8"
        }
    }
}
```

### Usage

1. Run `forge download` to fetch the dependencies into your project.
2. Run `forge build`. Forge will automatically generate the `FetchContent_Declare` and `FetchContent_MakeAvailable` calls in your `CMakeLists.txt`.
3. Link the dependency in your code. Forge handles the target linking if it can identify the target name, otherwise, you might need to use `forge.add_cmake` to manually link.

---

## Conan Packages

Forge has built-in support for [Conan](https://conan.io/), a powerful C/C++ package manager.

### Prerequisites

You must have `conan` installed on your system and configured. Forge will use the `conan` executable.

### Configuration

Add your Conan packages to the `dependencies.conan` table:

```lua
dependencies = {
    conan = {
        ["nlohmann_json"] = "3.11.2",
        ["spdlog"] = "1.12.0"
    }
}
```

### Usage

1. Run `forge build`. 
2. Forge will automatically generate a `conanfile.txt` in the `.config/` directory.
3. It then runs `conan install`.
4. Forge parses the output from Conan to identify the necessary `find_package` calls and `target_link_libraries` targets.
5. These are then injected into the generated `CMakeLists.txt`.

### How it works under the hood

Forge uses the `CMakeDeps` and `CMakeToolchain` generators from Conan. It points CMake to the generated toolchain file located in `build/build/Release/generators/conan_toolchain.cmake`.

---

## Which one should I use?

- **Use Direct Git Dependencies** when you want to build the dependency from source alongside your project, or when a Conan package is not available.
- **Use Conan Packages** for large libraries that take a long time to build, or when you want to use pre-compiled binaries for your specific platform/compiler.
