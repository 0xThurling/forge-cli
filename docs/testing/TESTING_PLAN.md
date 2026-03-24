# Forge Testing Plan

## Table of Contents

1. [Overview](#overview)
2. [Test Categories](#test-categories)
3. [Project Type Scenarios](#project-type-scenarios)
4. [Build and Integration Tests](#build-and-integration-tests)
5. [Running Tests](#running-tests)
6. [Test Environment Setup](#test-environment-setup)
7. [Expected Results](#expected-results)
8. [Troubleshooting](#troubleshooting)

---

## Overview

This document outlines the comprehensive testing plan for Forge, a C++ project management tool. The testing plan covers multiple scenarios including different project types (graphics, fullstack, libraries), build verification, and integration testing across supported platforms.

**Supported Platforms**:
- Linux (x64)
- macOS (x64, arm64)
- Windows (x64)

---

## Test Categories

### 1. Unit Tests
- Internal C# tests for Forge commands and utilities
- Tests for configuration parsing (package.toml)
- Tests for CMake generation logic

### 2. Integration Tests
- End-to-end project creation and build workflows
- Dependency resolution with Conan
- Resource embedding verification

### 3. Project Type Tests
- Executable projects
- Library projects (static/shared)
- Graphics projects (SDL2, OpenGL, Vulkan)
- Fullstack projects (with web servers or APIs)

### 4. Platform Tests
- Linux-specific builds
- macOS-specific builds
- Windows-specific builds

---

## Project Type Scenarios

### Scenario 1: Basic Executable Project

**Purpose**: Verify basic project creation and build functionality.

**Test Steps**:

```bash
# 1. Create new executable project
forge create test_executable

# 2. Navigate to project
cd test_executable

# 3. Build the project
forge build

# 4. Run the executable
forge run

# 5. Clean build artifacts
forge clean
```

**Verification Points**:
- [ ] Directory structure created correctly
- [ ] package.toml generated with correct defaults
- [ ] CMakeLists.txt generated successfully
- [ ] Build completes without errors
- [ ] Executable runs and outputs expected text

**Expected Output**:
```
Hello, C++ World!
```

---

### Scenario 2: Library Project

**Purpose**: Verify library project creation and build.

**Test Steps**:

```bash
# 1. Create library project
forge create test_library --type library

# 2. Navigate to project
cd test_library

# 3. Build the project
forge build

# 4. Verify library was created
ls -la build/
```

**Verification Points**:
- [ ] Library type set correctly in package.toml
- [ ] Static library (.a or .lib) generated
- [ ] CMakeLists.txt configured as library

---

### Scenario 3: Graphics Project (SDL2)

**Purpose**: Verify graphics library integration with SDL2.

**Test Steps**:

```bash
# 1. Create executable project
forge create test_graphics

# 2. Navigate to project
cd test_graphics

# 3. Add SDL2 as dependency (edit package.toml)
# package.toml should contain:
# [dependencies]
# sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }

# 4. Build the project
forge build --verbose
```

**Verification Points**:
- [ ] SDL2 fetched via CMake FetchContent
- [ ] Build completes without linker errors
- [ ] Test executable runs (headless test or display required)

---

### Scenario 4: Graphics Project (OpenGL)

**Purpose**: Verify OpenGL graphics project setup.

**Test Steps**:

```bash
# 1. Create project
forge create test_opengl

# 2. Add OpenGL dependencies
# On Linux (Ubuntu/Debian):
# forge run  # This will run pre-build script if defined

# 3. Create source file with OpenGL code
# src/main.cpp should include OpenGL headers

# 4. Build
forge build --verbose
```

**Verification Points**:
- [ ] OpenGL headers found
- [ ] Library linking successful
- [ ] Build completes

---

### Scenario 5: Fullstack C++ Project

**Purpose**: Verify C++ project with server/API components.

**Test Steps**:

```bash
# 1. Create project
forge create test_fullstack

# 2. Add dependencies (e.g., Boost, nlohmann_json)
# Edit package.toml:
# [conan-dependencies]
# nlohmann_json = "3.10.5"
# boost = "1.85.0"

# 3. Install Conan dependencies
forge install

# 4. Build
forge build
```

**Verification Points**:
- [ ] Conan dependencies resolved correctly
- [ ] nlohmann_json available for use
- [ ] CMake finds Boost components

---

### Scenario 6: Project with Resources

**Purpose**: Verify resource embedding system.

**Test Steps**:

```bash
# 1. Create project
forge create test_resources

# 2. Add a resource file
mkdir -p test_resources/assets
echo "test data" > test_resources/assets/config.json

# 3. Register the resource
cd test_resources
forge embed assets/config.json

# 4. Build
forge build

# 5. Verify embedded_resources.h and .cpp generated
ls -la src/
```

**Verification Points**:
- [ ] Resource added to package.toml
- [ ] embedded_resources.h generated
- [ ] embedded_resources.cpp generated with correct data
- [ ] Resource accessible via Embedded::get()

---

### Scenario 7: Project with Tests

**Purpose**: Verify Google Test integration.

**Test Steps**:

```bash
# 1. Create project
forge create test_with_tests

# 2. Navigate to project
cd test_with_tests

# 3. Add googletest dependency
# Edit package.toml:
# [dependencies]
# googletest = { git = "https://github.com/google/googletest.git", tag = "release-1.12.1", target = "GTest::gtest" }

# 4. Create test files
mkdir -p test
cat > test/main.cpp << 'EOF'
#include <gtest/gtest.h>

TEST(MathTest, Addition) {
    EXPECT_EQ(1 + 1, 2);
}

TEST(MathTest, Multiplication) {
    EXPECT_EQ(3 * 4, 12);
}

int main(int argc, char** argv) {
    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
EOF

# 5. Run tests
forge test
```

**Verification Points**:
- [ ] Test directory created
- [ ] CMake enables testing
- [ ] Tests compile and link
- [ ] Tests run successfully

---

### Scenario 8: Project with Pre/Post Build Scripts

**Purpose**: Verify build script execution.

**Test Steps**:

```bash
# 1. Create project
forge create test_scripts

# 2. Edit package.toml to add scripts:
# [scripts]
# pre-build = "echo 'Pre-build: Starting...'"
# post-build = "echo 'Post-build: Complete!'"

# 3. Build
forge build
```

**Verification Points**:
- [ ] Pre-build script runs before CMake
- [ ] Post-build script runs after CMake build
- [ ] Scripts execute in correct order

---

### Scenario 9: Multi-Dependency Project

**Purpose**: Verify complex dependency scenarios.

**Test Steps**:

```bash
# 1. Create project
forge create test_multi_deps

# 2. Edit package.toml with multiple dependencies:
# [dependencies]
# sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
# fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
# 
# [conan-dependencies]
# spdlog = "1.12.0"

# 3. Install Conan dependencies
forge install

# 4. Build
forge build --verbose
```

**Verification Points**:
- [ ] Git dependencies fetched via FetchContent
- [ ] Conan dependencies resolved
- [ ] All dependencies linked correctly
- [ ] No symbol conflicts

---

### Scenario 10: Custom C++ Standard

**Purpose**: Verify C++ standard configuration.

**Test Steps**:

```bash
# 1. Create project
forge create test_cpp17

# 2. Build with specific standard
forge build --standard 17
```

**Verification Points**:
- [ ] CMAKE_CXX_STANDARD set to 17
- [ ] Project compiles with C++17 features

---

## Build and Integration Tests

### Test Suite 1: Core Commands

| Command | Test | Expected Result |
|---------|------|-----------------|
| `forge create` | Create new project | Project directories and files created |
| `forge build` | Build existing project | CMake generates, build succeeds |
| `forge run` | Run executable | Application executes |
| `forge test` | Run test suite | Tests execute and pass |
| `forge clean` | Clean build artifacts | build/ directory removed |
| `forge embed` | Register resource | Resource added to config |

### Test Suite 2: Code Generation

| Command | Test | Expected Result |
|---------|------|-----------------|
| `forge new class Player` | Generate class | Player.h and Player.cpp created |
| `forge new struct Point` | Generate struct | Point.h created |
| `forge new header util` | Generate header | util.h created with guards |
| `forge new source math` | Generate source pair | math.h and math.cpp created |

### Test Suite 3: Project Information

| Command | Test | Expected Result |
|---------|------|-----------------|
| `forge project info` | Display project info | Correct configuration displayed |
| `forge project tree` | Display file tree | Directory structure shown |
| `forge project stats` | Display statistics | File counts correct |
| `forge project dependencies` | List dependencies | All deps listed |
| `forge project scripts` | List scripts | Available scripts shown |

---

## Running Tests

### Running All Project Scenarios

```bash
#!/bin/bash
# Run all test scenarios sequentially

SCENARIOS=(
    "test_executable"
    "test_library"
    "test_graphics"
    "test_fullstack"
    "test_resources"
    "test_with_tests"
    "test_scripts"
    "test_multi_deps"
    "test_cpp17"
)

for scenario in "${SCENARIOS[@]}"; do
    echo "=== Testing: $scenario ==="
    forge create "$scenario"
    cd "$scenario"
    forge build
    cd ..
    forge clean
done
```

### Running Specific Test Category

```bash
# Graphics tests only
forge create graphics_test
cd graphics_test
# Add graphics dependencies manually
forge build --verbose

# Fullstack tests only
forge create fullstack_test
cd fullstack_test
# Add server dependencies
forge install
forge build
```

### Running with Verbose Output

```bash
# For detailed debugging
forge build --verbose

# For test debugging
forge test --filter="*TestName*"
```

---

## Test Environment Setup

### Prerequisites

Before running tests, ensure the following are installed:

| Tool | Version | Purpose |
|------|---------|---------|
| CMake | >= 3.23 | Build system |
| Conan | >= 2.0 | Package manager |
| Git | Any | Version control |
| C++ Compiler | GCC/Clang/MSVC | Compilation |
| Google Test | 1.12.1+ | Testing framework |

### Installation Commands

**Linux (Ubuntu/Debian)**:
```bash
sudo apt-get update
sudo apt-get install cmake g++ git
pip install conan
```

**macOS**:
```bash
brew install cmake git
pip install conan
```

**Windows**:
```powershell
winget install CMake.Git.Conan
```

---

## Expected Results

### Successful Build Output

```
=== Building project ===
Loading package.toml...
Installing Conan dependencies...
Generating CMakeLists.txt...
CMake configure: Done
CMake build: Done
Build complete!
```

### Successful Test Output

```
=== Running Tests ===
[==========] Running 2 tests from 1 test suite.
[----------] Global test environment set-up.
[----------] 2 tests from 1 test suite.
[----------] MathTest.Addition
[       OK ] MathTest.Addition (0 ms)
[----------] MathTest.Multiplication
[       OK ] MathTest.Multiplication (0 ms)
[----------] Global test environment tear-down
[==========] 2 tests from 1 test suite ran. (0 ms total)

All tests passed.
```

### Error Outputs

| Scenario | Error Message |
|----------|---------------|
| No CMake | `Error: cmake command not found` |
| No package.toml | `Error: Not a forge project` |
| No Conan | `Error: conan command not found` |
| Build failure | `Error: Build failed` |
| Test failure | `Error: Tests failed` |

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| CMake not found | CMake not installed | Install CMake |
| package.toml not found | Not in project root | Run from project directory |
| Conan not found | Conan not installed | Install Conan |
| Build fails | Missing dependency | Check package.toml |
| Resource not found | Wrong path | Use relative paths |
| Tests fail | GTest not configured | Add googletest to deps |

### Debug Commands

```bash
# Check project configuration
forge project info

# View dependency tree
forge project dependencies

# List available scripts
forge project scripts

# Verbose build output
forge build --verbose
```

### Test Isolation

Each test scenario should be isolated:
- Use separate project directories
- Run `forge clean` between tests
- Remove test projects after verification

```bash
# Cleanup after testing
rm -rf test_*
```

---

## Platform-Specific Notes

### Linux

- Default generators work correctly
- Package managers: apt-get, pacman, brew
- Test display: May need X server for graphics tests

### macOS

- Default generators work correctly
- Homebrew for system packages
- Architecture: x64 and arm64 (Apple Silicon)

### Windows

- Default generator may need adjustment
- Package managers: winget, choco
- Visual Studio or MinGW for compilers

---

## Appendix: Test Checklist

- [ ] Basic executable project creation and build
- [ ] Library project creation and build
- [ ] SDL2 graphics project build
- [ ] OpenGL graphics project build
- [ ] Fullstack project with Conan dependencies
- [ ] Resource embedding verification
- [ ] Google Test integration
- [ ] Pre/post build scripts execution
- [ ] Multiple dependency resolution
- [ ] Custom C++ standard (17, 20)
- [ ] Code generation commands (class, struct, header, source)
- [ ] Project information commands (info, tree, stats, dependencies, scripts)
- [ ] Clean command verification
- [ ] Cross-platform testing (Linux, macOS, Windows)
