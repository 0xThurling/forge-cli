# Forge Lua Testing Plan

## Overview

This document covers testing scenarios for the Lua engine integration with the modular CMake generator system. The Lua engine enables custom CMake injection via the `forge.add_cmake()` function.

**Reference**: See [IMPLEMENTATION_PLAN.md](../planning/IMPLEMENTATION_PLAN.md)
**Quick Reference**: See [QUICK_REFERENCE.md](../planning/QUICK_REFERENCE.md)

---

## Lua API Reference

### Available Functions

```lua
-- Logging
forge.log.info("message")

-- Git
forge.pull_repo("https://github.com/user/repo.git")

-- System packages
forge.get_packages("nopass", forge.package_manager.aptget, {"package"})

-- Custom CMake (key feature!)
forge.add_cmake("cmake_command")

-- Environment
forge.os.current           -- "linux", "macos", "windows"
forge.distro.my_distro    -- detected Linux distro
forge.current_working_dir -- current working directory
```

### Custom CMake Placeholders

| Placeholder | Replaced With |
|-------------|---------------|
| `${PROJECT_NAME}` | Actual project name |

---

## Test Category 1: Core Lua Functions

### Test 1.1: forge.add_cmake() Function

**Purpose**: Verify custom CMake injection via Lua.

**Test Steps**:

```bash
# 1. Create project
forge create test_add_cmake
cd test_add_cmake

# 2. Create Lua build script
mkdir -p .config/forge/build
cat > .config/forge/build/test.lua << 'EOF'
forge.log.info("Adding custom CMake...")

forge.add_cmake([[
# Custom CMake from Lua
message(STATUS "Hello from custom CMake!")
]])

forge.log.info("Custom CMake added successfully!")
EOF

# 3. Build
forge build
```

**Verification Points**:
- [ ] Lua script executes without error
- [ ] Custom CMake appears in `.config/cmake/CMakeLists.txt`
- [ ] Message displays during CMake configure

---

### Test 1.2: PROJECT_NAME Placeholder

**Purpose**: Verify `${PROJECT_NAME}` replacement.

**Test Steps**:

```bash
# 1. Create project
forge create test_placeholder

# 2. Create Lua script with placeholder
cat > test_placeholder/.config/forge/build/placeholder.lua << 'EOF'
forge.add_cmake([[
target_compile_definitions(${PROJECT_NAME} PRIVATE MY_CUSTOM_FLAG)
message(STATUS "Added define to ${PROJECT_NAME}")
]])
EOF

# 3. Build
cd test_placeholder
forge build

# 4. Check generated CMake
cat .config/cmake/CMakeLists.txt
```

**Expected Output**:
```cmake
target_compile_definitions(test_placeholder PRIVATE MY_CUSTOM_FLAG)
message(STATUS "Added define to test_placeholder")
```

**Verification Points**:
- [ ] `${PROJECT_NAME}` replaced with actual project name
- [ ] Generated CMake is syntactically valid

---

### Test 1.3: Multiple Custom CMake Snippets

**Purpose**: Verify multiple Lua scripts can add CMake.

**Test Steps**:

```bash
# 1. Create multiple Lua scripts
cat > .config/forge/build/script_a.lua << 'EOF'
forge.add_cmake([[
# From script A
add_definitions(FROM_SCRIPT_A)
]])
forge.log.info("Script A executed")
EOF

cat > .config/forge/build/script_b.lua << 'EOF'
forge.add_cmake([[
# From script B  
add_definitions(FROM_SCRIPT_B)
]])
forge.log.info("Script B executed")
EOF

# 2. Build
forge build
```

**Verification Points**:
- [ ] Both snippets appear in generated CMake
- [ ] Both log messages appear in output

---

### Test 1.4: Custom CMake Position in Priority Order

**Purpose**: Verify CustomCMakeSection appears at priority 45.

**Test Steps**:

```bash
# 1. Create project with deps and custom CMake
forge create test_priority_cmake

cat > test_priority_cmake/package.toml << 'EOF'
[project]
name = "test_priority_cmake"
type = "executable"

[dependencies]
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
EOF

cat > test_priority_cmake/.config/forge/build/custom.lua << 'EOF'
forge.add_cmake([[
# Custom CMake at priority 45
message(STATUS "Custom CMake section")
]])
EOF

# 2. Build
cd test_priority_cmake
forge build

# 3. Check order
grep -n "CMAKE_CXX_STANDARD\|FetchContent_Declare\|add_executable\|target_link_libraries\|Custom CMake\|enable_testing" .config/cmake/CMakeLists.txt
```

**Expected Order**:
1. StandardSection (1) - CMAKE_CXX_STANDARD
2. FetchContentSection (10) - FetchContent_Declare
3. ProjectTargetSection (30) - add_executable
4. LinkingSection (40) - target_link_libraries
5. **CustomCMakeSection (45)** - Custom CMake
6. TestingSection (50) - enable_testing

**Verification Points**:
- [ ] Custom CMake appears after LinkingSection
- [ ] Custom CMake appears before TestingSection

---

## Test Category 2: Platform Detection via Lua

### Test 2.1: OS Detection

**Purpose**: Verify `forge.os.current` returns correct OS.

**Test Steps**:

```bash
# Create Lua script to detect OS
cat > .config/forge/build/detect_os.lua << 'EOF'
forge.log.info("Current OS: " .. forge.os.current)
forge.log.info("Is Linux: " .. tostring(forge.os.linux))
forge.log.info("Is macOS: " .. tostring(forge.os.macos))
forge.log.info("Is Windows: " .. tostring(forge.os.windows))
EOF

forge build
```

**Verification Points**:
- [ ] `forge.os.current` returns "linux", "macos", or "windows"
- [ ] `forge.os.linux` is true on Linux
- [ ] `forge.os.macos` is true on macOS
- [ ] `forge.os.windows` is true on Windows

---

### Test 2.2: Linux Distribution Detection

**Purpose**: Verify `forge.distro.my_distro` returns correct distro.

**Test Steps**:

```bash
cat > .config/forge/build/detect_distro.lua << 'EOF'
local distro = forge.distro.my_distro
forge.log.info("Detected distribution: " .. distro)

-- Check specific distros
forge.log.info("Is Ubuntu: " .. tostring(forge.distro.ubuntu))
forge.log.info("Is Debian: " .. tostring(forge.distro.debian))
forge.log.info("Is Arch: " .. tostring(forge.distro.arch))
forge.log.info("Is Fedora: " .. tostring(forge.distro.fedora))
EOF

forge build
```

**Verification Points**:
- [ ] `forge.distro.my_distro` returns correct distro name
- [ ] Specific distro flags return correct boolean

---

## Test Category 3: Git Integration via Lua

### Test 3.1: forge.pull_repo()

**Purpose**: Verify repository cloning.

**Test Steps**:

```bash
# 1. Create project
forge create test_pull

# 2. Create Lua script
cat > test_pull/.config/forge/build/git_clone.lua << 'EOF'
forge.log.info("Cloning repository...")

-- Clone a small header-only library
forge.pull_repo("https://github.com/fmtlib/fmt.git")

forge.log.info("Clone complete!")
EOF

# 3. Build
cd test_pull
forge build

# 4. Verify
ls -la external/
```

**Verification Points**:
- [ ] Repository cloned to external/ directory
- [ ] No git errors

---

### Test 3.2: Git Clone with CMake Integration

**Purpose**: Test complete workflow of cloning + CMake usage.

**Test Steps**:

```bash
# 1. Create project
forge create test_git_cmake

# 2. Lua script that clones and adds CMake
cat > test_git_cmake/.config/forge/build/glm_setup.lua << 'EOF'
forge.log.info("Setting up GLM...")

-- Clone GLM (header-only)
forge.pull_repo("https://github.com/g-truc/glm.git")

-- Add include directory
forge.add_cmake([[
target_include_directories(${PROJECT_NAME} PRIVATE ${PROJECT_SOURCE_DIR}/external/glm)
]])

forge.log.info("GLM setup complete!")
EOF

# 3. Create source using GLM
cat > test_git_cmake/src/main.cpp << 'EOF'
#include <glm/glm.hpp>
#include <iostream>

int main() {
    glm::vec4 vec(1.0f);
    std::cout << "GLM test: " << vec.x << std::endl;
    return 0;
}
EOF

# 4. Build
cd test_git_cmake
forge build
```

**Verification Points**:
- [ ] GLM cloned to external/glm
- [ ] Custom CMake adds include directory
- [ ] Project compiles with GLM

---

## Test Category 4: System Package Installation via Lua

### Test 4.1: Ubuntu/Debian Package Installation

**Purpose**: Verify `forge.get_packages()` on Ubuntu.

**Test Steps**:

```bash
# Create Lua script for Ubuntu
cat > .config/forge/build/ubuntu_deps.lua << 'EOF'
if forge.os.linux then
    local distro = forge.distro.my_distro
    if distro == "ubuntu" or distro == "debian" then
        forge.log.info("Installing packages via apt-get...")
        forge.get_packages("nopass", forge.package_manager.aptget, {"libxrandr-dev"})
        forge.log.info("Packages installed!")
    end
end
EOF

forge build
```

**Verification Points**:
- [ ] OS detection works
- [ ] Distro detection works
- [ ] Package manager identified

---

### Test 4.2: macOS Package Installation

**Purpose**: Verify `forge.get_packages()` on macOS.

**Test Steps**:

```bash
# Create Lua script for macOS
cat > .config/forge/build/macos_deps.lua << 'EOF'
if forge.os.macos then
    forge.log.info("Installing packages via Homebrew...")
    forge.get_packages("nopass", forge.package_manager.brew, {"sdl2"})
    forge.log.info("Homebrew packages installed!")
end
EOF

forge build
```

**Verification Points**:
- [ ] macOS detected correctly
- [ ] Homebrew package manager identified

---

### Test 4.3: Cross-Platform Package Script

**Purpose**: Test unified script for all platforms.

**Test Steps**:

```bash
cat > .config/forge/build/cross_platform_packages.lua << 'EOF'
local os_name = forge.os.current
forge.log.info("Setting up for: " .. os_name)

if os_name == "linux" then
    local distro = forge.distro.my_distro
    if distro == "ubuntu" or distro == "debian" then
        forge.get_packages("nopass", forge.package_manager.aptget, {"libxrandr-dev"})
    elseif distro == "arch" then
        forge.get_packages("nopass", forge.package_manager.pacman, {"libxrandr"})
    end
elseif os_name == "macos" then
    forge.get_packages("nopass", forge.package_manager.brew, {"xrandr"})
elseif os_name == "windows" then
    forge.log.info("Windows: packages managed via vcpkg or manual")
end

forge.log.info("Package setup complete!")
EOF

forge build
```

**Verification Points**:
- [ ] All platforms handled correctly
- [ ] Correct package manager per platform

---

## Test Category 5: Graphics Project Scenarios

### Test 5.1: SDL2 with Lua Setup

**Purpose**: Test SDL2 integration with Lua system package installation.

**Test Steps**:

```bash
# 1. Create project
forge create test_sdl2_lua

# 2. Add SDL2 to package.toml
cat > test_sdl2_lua/package.toml << 'EOF'
[project]
name = "test_sdl2_lua"
type = "executable"

[dependencies]
sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
EOF

# 3. Add Lua setup script
cat > test_sdl2_lua/.config/forge/build/sdl2_setup.lua << 'EOF'
forge.log.info("Setting up SDL2...")

if forge.os.linux then
    local distro = forge.distro.my_distro
    if distro == "ubuntu" or distro == "debian" then
        forge.get_packages("nopass", forge.package_manager.aptget, {
            "libsdl2-dev",
            "libsdl2-image-dev"
        })
    end
end

forge.log.info("SDL2 setup complete!")
EOF

# 4. Build
cd test_sdl2_lua
forge build
```

**Verification Points**:
- [ ] SDL2 fetched via FetchContent
- [ ] System packages installed (if permission)
- [ ] Build succeeds

---

### Test 5.2: WebGPU with Lua

**Purpose**: Test WebGPU setup using Lua scripts.

**Test Steps**:

```bash
# 1. Create project
forge create test_webgpu_lua

# 2. Download WebGPU distribution
cd test_webgpu_lua
curl -L -o webgpu.zip "https://github.com/eliemichel/WebGPU-distribution/archive/refs/tags/wgpu-v24.0.0.2.zip"
unzip -o webgpu.zip
mv WebGPU-distribution-* webgpu
rm webgpu.zip

# 3. Add Lua script for WebGPU setup
cat > test_webgpu_lua/.config/forge/build/webgpu_setup.lua << 'EOF'
forge.log.info("Setting up WebGPU...")

if forge.os.linux then
    local distro = forge.distro.my_distro
    if distro == "ubuntu" or distro == "debian" then
        forge.get_packages("nopass", forge.package_manager.aptget, {
            "libxrandr-dev",
            "libxinerama-dev",
            "libxcursor-dev",
            "mesa-common-dev"
        })
    end
end

-- Add WebGPU subdirectory
forge.add_cmake([[
add_subdirectory(webgpu)
target_link_libraries(${PROJECT_NAME} PRIVATE webgpu)
message(STATUS "WebGPU linked")
]])

forge.log.info("WebGPU setup complete!")
EOF

# 4. Add minimal source
cat > test_webgpu_lua/src/main.cpp << 'EOF'
#define WEBGPU_BACKEND_WGPU
#include <webgpu/webgpu.h>
#include <iostream>

int main() {
    WGPUInstanceDescriptor desc = {};
    WGPUInstance instance = wgpuCreateInstance(&desc);
    if (!instance) {
        std::cerr << "WebGPU not available" << std::endl;
        return 1;
    }
    std::cout << "WebGPU instance created successfully" << std::endl;
    wgpuInstanceRelease(instance);
    return 0;
}
EOF

# 5. Update package.toml
cat > test_webgpu_lua/package.toml << 'EOF'
[project]
name = "test_webgpu_lua"
type = "executable"
EOF

# 6. Build
cd test_webgpu_lua
forge build
```

**Verification Points**:
- [ ] WebGPU system deps installed
- [ ] add_subdirectory(webgpu) in generated CMake
- [ ] target_link_libraries includes webgpu

---

### Test 5.3: OpenGL with Lua

**Purpose**: Test OpenGL project with platform-specific setup.

**Test Steps**:

```bash
# 1. Create project
forge create test_opengl_lua

# 2. Create Lua script
cat > test_opengl_lua/.config/forge/build/opengl.lua << 'EOF'
forge.log.info("Setting up OpenGL...")

if forge.os.linux then
    local packages = {}
    local distro = forge.distro.my_distro
    
    if distro == "ubuntu" or distro == "debian" then
        packages = {"libgl1-mesa-dev", "libglu1-mesa-dev", "libxrandr-dev", "libxinerama-dev"}
    elseif distro == "arch" or distro == "manjaro" then
        packages = {"mesa", "glu", "libxrandr", "libxinerama"}
    end
    
    if #packages > 0 then
        forge.get_packages("nopass", forge.package_manager.aptget, packages)
    end
end

forge.add_cmake([[
find_package(OpenGL REQUIRED)
target_link_libraries(${PROJECT_NAME} PRIVATE OpenGL::GL)
]])

forge.log.info("OpenGL setup complete!")
EOF

# 3. Build
cd test_opengl_lua
forge build
```

**Verification Points**:
- [ ] Correct packages installed per distro
- [ ] find_package(OpenGL) in generated CMake
- [ ] OpenGL::GL linked

---

## Test Category 6: Complex Scenarios

### Test 6.1: Conditional CMake via Environment Variables

**Purpose**: Test CMake generation based on environment variables.

**Test Steps**:

```bash
# 1. Create Lua script that reads env vars
cat > .config/forge/build/conditional.lua << 'EOF'
local use_debug = os.getenv("DEBUG_BUILD")
local use_coverage = os.getenv("COVERAGE")

forge.log.info("DEBUG_BUILD: " .. (use_debug or "not set"))
forge.log.info("COVERAGE: " .. (use_coverage or "not set"))

if use_debug == "ON" then
    forge.add_cmake([[
        set(CMAKE_BUILD_TYPE Debug)
    ]])
end

if use_coverage == "ON" then
    forge.add_cmake([[
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} --coverage")
    ]])
end
EOF

# 2. Build with environment variables
DEBUG_BUILD=ON COVERAGE=ON forge build
```

**Verification Points**:
- [ ] Environment variables read correctly
- [ ] Conditional CMake added based on variables

---

### Test 6.2: Multiple Dependencies with Custom CMake

**Purpose**: Test complex project with multiple deps and custom CMake.

**Test Steps**:

```bash
# 1. Create project
forge create test_complex

# 2. package.toml
cat > test_complex/package.toml << 'EOF'
[project]
name = "test_complex"
type = "executable"

[dependencies]
sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }

[conan-dependencies]
spdlog = "1.12.0"
EOF

# 3. Lua setup
cat > test_complex/.config/forge/build/setup.lua << 'EOF'
-- Install system deps
if forge.os.linux and forge.distro.my_distro == "ubuntu" then
    forge.get_packages("nopass", forge.package_manager.aptget, {"libsdl2-dev"})
end

-- Add custom CMake
forge.add_cmake([[
# Custom compile options
target_compile_options(${PROJECT_NAME} PRIVATE -Wall -Wextra)
]])

forge.log.info("Complex setup complete!")
EOF

# 4. Install Conan deps
cd test_complex
forge install

# 5. Build
forge build
```

**Verification Points**:
- [ ] All FetchContent deps work
- [ ] Conan deps resolved
- [ ] Custom CMake combines with all sections
- [ ] Priority order maintained

---

### Test 6.3: CMakeGenerator Full Integration

**Purpose**: Complete end-to-end test of CMakeRegistry + Lua.

**Test Steps**:

```bash
# 1. Create full-featured project
forge create test_full_integration

# 2. Add all configuration
cat > test_full_integration/package.toml << 'EOF'
[project]
name = "test_full_integration"
type = "executable"
standard = "20"

[dependencies]
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
googletest = { git = "https://github.com/google/googletest.git", tag = "release-1.12.1", target = "GTest::gtest" }

[conan-dependencies]
spdlog = "1.12.0"
EOF

# 3. Create test files
mkdir -p test_full_integration/test
cat > test_full_integration/test/main.cpp << 'EOF'
#include <gtest/gtest.h>
#include <fmt/core.h>

TEST(FullTest, Format) {
    EXPECT_EQ(fmt::format("{}", 42), "42");
}

TEST(FullTest, Basic) {
    EXPECT_TRUE(true);
}

int main(int argc, char** argv) {
    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
EOF

# 4. Create Lua build script
mkdir -p test_full_integration/.config/forge/build
cat > test_full_integration/.config/forge/build/full.lua << 'EOF'
forge.log.info("Full integration test...")

-- Add custom compile definitions
forge.add_cmake([[
# Custom definitions for full integration test
add_compile_definitions(FULL_INTEGRATION_TEST)
message(STATUS "Full integration: custom CMake added")
]])

forge.log.info("Full integration setup complete!")
EOF

# 5. Install Conan deps
cd test_full_integration
forge install

# 6. Build
forge build

# 7. Run tests
forge test
```

**Verification Points**:
- [ ] All 7 CMake sections generated
- [ ] Priority order correct
- [ ] Lua custom CMake at priority 45
- [ ] Build completes successfully
- [ ] Tests run and pass
- [ ] Custom definitions in compiled binary

---

## Test Checklist

### Core Lua Functions
- [ ] forge.add_cmake() adds to CustomCMakeSnippets
- [ ] ${PROJECT_NAME} placeholder replaced correctly
- [ ] Multiple snippets accumulate
- [ ] CustomCMakeSection appears at priority 45

### Platform Detection
- [ ] forge.os.current returns correct OS
- [ ] forge.os.linux/macos/windows booleans work
- [ ] forge.distro.my_distro returns correct distro
- [ ] Distro-specific flags work

### Git Integration
- [ ] forge.pull_repo() clones to external/
- [ ] Cloned repos usable in CMake

### Package Installation
- [ ] forge.get_packages() works on Ubuntu
- [ ] forge.get_packages() works on macOS
- [ ] Correct package manager per platform

### Graphics Projects
- [ ] SDL2 with Lua setup works
- [ ] WebGPU with Lua setup works
- [ ] OpenGL with Lua setup works

### Complex Scenarios
- [ ] Environment variables read correctly
- [ ] Multiple dependencies + custom CMake works
- [ ] Full integration (all sections + Lua) works

### Priority Order Verification
- [ ] StandardSection (1) before FetchContent (10)
- [ ] FetchContent (10) before ProjectTarget (30)
- [ ] ProjectTarget (30) before Linking (40)
- [ ] Linking (40) before CustomCMake (45)
- [ ] CustomCMake (45) before Testing (50)
