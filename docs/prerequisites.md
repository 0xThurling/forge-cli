# Prerequisites

To use **Forge** effectively, you need to have several tools installed and configured on your system. Forge acts as an orchestrator for these tools to simplify your C++ development workflow.

## Essential Tools

### 1. C++ Compiler
You need a modern C++ compiler that supports at least C++11, though C++20 is recommended as it is the Forge default.
- **Linux**: GCC or Clang
- **macOS**: Clang (via Xcode Command Line Tools)
- **Windows**: MSVC (via Visual Studio) or MinGW/GCC

### 2. CMake (v3.23 or higher)
Forge generates CMake files and uses the `cmake` executable to configure and build your project.
- **Verification**: `cmake --version`
- **Installation**: [cmake.org/download](https://cmake.org/download/) or your system's package manager.

### 3. Git
Used for managing direct dependencies via CMake `FetchContent` and for Forge's `pull_repo` and `fetch` functions.
- **Verification**: `git --version`
- **Installation**: [git-scm.com](https://git-scm.com/)

---

## Optional (but Recommended) Tools

### 4. Conan (v2.x)
Required if you want to use Conan packages for dependency management.
- **Verification**: `conan --version`
- **Installation**: `pip install conan`

### 5. .NET Runtime (v8.0)
Forge is built using C# and .NET. You will need the .NET 8.0 runtime (or SDK) to run the `forge` executable.
- **Verification**: `dotnet --list-runtimes`
- **Installation**: [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Environment Check

You can use the built-in "doctor" command to check if your project environment is correctly set up:

```bash
forge doctor
```

While `forge doctor` primarily checks your `forge.lua` and project structure, it is a good first step to ensure everything is in order.

## Summary Checklist
- [ ] C++ Compiler (GCC/Clang/MSVC)
- [ ] CMake 3.23+
- [ ] Git
- [ ] Conan (optional)
- [ ] .NET 8.0 Runtime
