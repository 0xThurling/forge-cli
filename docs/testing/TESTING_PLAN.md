# Forge Testing Plan

## Overview

This document outlines the comprehensive testing plan for the Forge modular CMake generator system. The testing plan aligns with the Registry + Priority Pattern implemented in the CMake generation system.

**System Under Test**: Modular CMake Generator with Lua Integration
**Reference**: See [IMPLEMENTATION_PLAN.md](../planning/IMPLEMENTATION_PLAN.md)

---

## Test Priority System

The CMake generator uses a priority-based system where lower numbers appear earlier in the generated file.

| Priority | Section | Purpose |
|----------|---------|---------|
| 0 | FormatSection | Code formatting |
| 1 | StandardSection | C++ standard config |
| 5 | ConanSection | Conan packages |
| 10 | FetchContentSection | Git dependencies |
| 30 | ProjectTargetSection | add_executable/library |
| 40 | LinkingSection | target_link_libraries |
| 45 | CustomCMakeSection | From Lua scripts |
| 50 | TestingSection | Google Test |

---

## Test Categories

### 1. Section Registration Tests
### 2. Priority Order Tests
### 3. Lua Integration Tests
### 4. CMake Generation Tests
### 5. End-to-End Project Tests

---

## Test Category 1: Section Registration

### Test 1.1: Verify All Built-in Sections Register

**Purpose**: Ensure all CMake sections are properly registered in CMakeRegistry.

**Test Steps**:

```bash
# 1. Create test project
forge create test_sections

# 2. Build to trigger CMake generation
cd test_sections
forge build

# 3. Check generated CMakeLists.txt
cat .config/cmake/CMakeLists.txt
```

**Verification Points**:
- [ ] StandardSection generates (C++ standard)
- [ ] FetchContentSection generates (if dependencies exist)
- [ ] ProjectTargetSection generates
- [ ] LinkingSection generates
- [ ] TestingSection generates (if test/ and googletest exist)
- [ ] CustomCMakeSection generates (if Lua scripts add content)

---

### Test 1.2: Section Name Verification

**Purpose**: Verify each section has correct Name property.

**Verification Points**:
- [ ] `StandardSection.Name == "standard"`
- [ ] `FetchContentSection.Name == "fetchcontent"`
- [ ] `ConanSection.Name == "conan"`
- [ ] `ProjectTargetSection.Name == "project_target"`
- [ ] `LinkingSection.Name == "linking"`
- [ ] `TestingSection.Name == "testing"`
- [ ] `CustomCMakeSection.Name == "custom"`

---

## Test Category 2: Priority Order Tests

### Test 2.1: Priority Ordering Verification

**Purpose**: Verify sections appear in correct order in generated CMake.

**Test Steps**:

```bash
# 1. Create project with all features
forge create test_priority

# 2. Add dependencies to trigger multiple sections
cat > test_priority/package.toml << 'EOF'
[project]
name = "test_priority"
type = "executable"

[dependencies]
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
googletest = { git = "https://github.com/google/googletest.git", tag = "release-1.12.1", target = "GTest::gtest" }
EOF

# 3. Add test directory
mkdir -p test_priority/test
cat > test_priority/test/main.cpp << 'EOF'
#include <gtest/gtest.h>
TEST(BasicTest, Pass) { EXPECT_TRUE(true); }
int main(int argc, char** argv) { testing::InitGoogleTest(&argc, argv); return RUN_ALL_TESTS(); }
EOF

# 4. Build and check order
cd test_priority
forge build

# 5. Verify order in CMakeLists.txt
grep -n "CMAKE_CXX_STANDARD\|FetchContent_Declare\|add_executable\|target_link_libraries\|enable_testing\|Custom CMake" .config/cmake/CMakeLists.txt
```

**Expected Order**:
1. StandardSection (Priority 1)
2. FetchContentSection (Priority 10)
3. ProjectTargetSection (Priority 30)
4. LinkingSection (Priority 40)
5. CustomCMakeSection (Priority 45)
6. TestingSection (Priority 50)

**Verification Points**:
- [ ] Standard appears before FetchContent
- [ ] FetchContent appears before ProjectTarget
- [ ] ProjectTarget appears before Linking
- [ ] Linking appears before Custom CMake
- [ ] Custom CMake appears before Testing

---

### Test 2.2: Custom Priority Positioning

**Purpose**: Verify new sections can be inserted at specific priorities.

**Test Steps**:

```csharp
// Example: Add new section at priority 35
public class MyCustomSection : CMakeSectionBase
{
    public override string Name => "my_custom";
    public override int Priority => 35;  // Between ProjectTarget (30) and Linking (40)
}
```

**Verification Points**:
- [ ] Section appears in correct position
- [ ] Priority comparison works correctly

---

## Test Category 3: Lua Integration Tests

### Test 3.1: Custom CMake via Lua

**Purpose**: Verify `forge.add_cmake()` function works.

**Test Steps**:

```bash
# 1. Create project
forge create test_lua_cmake

# 2. Create Lua build script
mkdir -p test_lua_cmake/.config/forge/build
cat > test_lua_cmake/.config/forge/build/custom.lua << 'EOF'
forge.log.info("Testing custom CMake injection...")

forge.add_cmake([[
# This is custom CMake from Lua!
message(STATUS "Hello from custom CMake!")
]])

forge.log.info("Custom CMake added!")
EOF

# 3. Build
cd test_lua_cmake
forge build
```

**Verification Points**:
- [ ] Lua script executes
- [ ] Custom CMake appears in generated file
- [ ] Message displays during CMake configure

### Test 3.2: Multiple Custom CMake Snippets

**Purpose**: Verify multiple Lua scripts can add CMake.

**Test Steps**:

```bash
# 1. Create multiple Lua scripts
cat > .config/forge/build/script1.lua << 'EOF'
forge.add_cmake([[
# From script1
add_definitions(SCRIPT1_FLAG)
]])
EOF

cat > .config/forge/build/script2.lua << 'EOF'
forge.add_cmake([[
# From script2
add_definitions(SCRIPT2_FLAG)
]])
EOF

# 2. Build
forge build
```

**Verification Points**:
- [ ] Both snippets appear in generated CMake
- [ ] Order matches script execution order

### Test 3.3: PROJECT_NAME Placeholder Replacement

**Purpose**: Verify `${PROJECT_NAME}` is replaced correctly.

**Test Steps**:

```bash
# 1. Create Lua script with placeholder
cat > .config/forge/build/placeholder.lua << 'EOF'
forge.add_cmake([[
target_compile_definitions(${PROJECT_NAME} PRIVATE MY_DEFINE)
]])
EOF

# 2. Build
forge build
```

**Verification Points**:
- [ ] `${PROJECT_NAME}` replaced with actual project name
- [ ] Generated CMake is valid

---

## Test Category 4: CMake Generation Tests

### Test 4.1: StandardSection Generation

**Purpose**: Verify C++ standard is set correctly.

**Test Steps**:

```bash
# Build with different standards
forge create test_std11 && cd test_std11 && forge build --standard 11 && grep -q "CMAKE_CXX_STANDARD 11" .config/cmake/CMakeLists.txt
forge create test_std17 && cd test_std17 && forge build --standard 17 && grep -q "CMAKE_CXX_STANDARD 17" .config/cmake/CMakeLists.txt
forge create test_std20 && cd test_std20 && forge build --standard 20 && grep -q "CMAKE_CXX_STANDARD 20" .config/cmake/CMakeLists.txt
```

**Verification Points**:
- [ ] CMAKE_CXX_STANDARD set to 11 when --standard 11
- [ ] CMAKE_CXX_STANDARD set to 17 when --standard 17
- [ ] CMAKE_CXX_STANDARD set to 20 when --standard 20
- [ ] CMAKE_CXX_STANDARD_REQUIRED ON
- [ ] CMAKE_CXX_EXTENSIONS OFF

---

### Test 4.2: FetchContentSection Generation

**Purpose**: Verify Git dependencies generate correctly.

**Test Steps**:

```bash
# 1. Create project with git dependencies
forge create test_fetch

cat > test_fetch/package.toml << 'EOF'
[project]
name = "test_fetch"
type = "executable"

[dependencies]
sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
EOF

# 2. Build
cd test_fetch
forge build

# 3. Check CMake
cat .config/cmake/CMakeLists.txt
```

**Verification Points**:
- [ ] `include(FetchContent)` present
- [ ] `FetchContent_Declare()` for each dependency
- [ ] `FetchContent_MakeAvailable()` for each dependency
- [ ] Correct GIT_REPOSITORY URLs
- [ ] Correct GIT_TAG versions

---

### Test 4.3: ProjectTargetSection Generation

**Purpose**: Verify executable/library targets generated correctly.

**Test Steps**:

```bash
# Test executable
forge create test_exec
cd test_exec
forge build
grep -q "add_executable(test_exec" .config/cmake/CMakeLists.txt

# Test library
forge create test_lib --type library
cd test_lib
forge build
grep -q "add_library(test_lib STATIC" .config/cmake/CMakeLists.txt
```

**Verification Points**:
- [ ] Executable projects generate `add_executable()`
- [ ] Library projects generate `add_library() STATIC`
- [ ] GLOB_RECURSE SOURCES used correctly

---

### Test 4.4: LinkingSection Generation

**Purpose**: Verify dependencies are linked correctly.

**Test Steps**:

```bash
# Create project with dependencies
forge create test_linking

cat > test_linking/package.toml << 'EOF'
[project]
name = "test_linking"
type = "executable"

[dependencies]
sdl = { git = "https://github.com/libsdl-org/SDL.git", tag = "release-2.30.3", target = "SDL2::SDL2" }
EOF

cd test_linking
forge build
grep -q "target_link_libraries(test_linking PRIVATE" .config/cmake/CMakeLists.txt
```

**Verification Points**:
- [ ] `target_link_libraries()` generated
- [ ] Uses correct CMake targets from dependencies
- [ ] SDL2::SDL2 linked correctly

---

### Test 4.5: TestingSection Generation

**Purpose**: Verify Google Test integration.

**Test Steps**:

```bash
# 1. Create project with tests
forge create test_gtest

# 2. Add googletest to dependencies
cat > test_gtest/package.toml << 'EOF'
[project]
name = "test_gtest"
type = "executable"

[dependencies]
googletest = { git = "https://github.com/google/googletest.git", tag = "release-1.12.1", target = "GTest::gtest" }
EOF

# 3. Create test directory and file
mkdir -p test_gtest/test
cat > test_gtest/test/main.cpp << 'EOF'
#include <gtest/gtest.h>

TEST(MathTest, Addition) {
    EXPECT_EQ(1 + 1, 2);
}

int main(int argc, char** argv) {
    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
EOF

# 4. Build
cd test_gtest
forge build
```

**Verification Points**:
- [ ] `enable_testing()` present
- [ ] TEST_SOURCES glob correct
- [ ] `add_executable(run_tests ...)` present
- [ ] `gtest_discover_tests(run_tests)` present

---

### Test 4.6: ConanSection Generation

**Purpose**: Verify Conan dependencies generate correctly.

**Test Steps**:

```bash
# 1. Create project with Conan deps
forge create test_conan

cat > test_conan/package.toml << 'EOF'
[project]
name = "test_conan"
type = "executable"

[conan-dependencies]
fmt = "10.2.1"
spdlog = "1.12.0"
EOF

# 2. Install Conan dependencies
cd test_conan
forge install

# 3. Build
forge build
```

**Verification Points**:
- [ ] `find_package()` calls generated for each Conan dep
- [ ] ConanSection only enabled when FindDependencies not empty

---

## Test Category 5: End-to-End Project Tests

### Test 5.1: Minimal Project

**Purpose**: Test basic project without special features.

```bash
forge create test_minimal
cd test_minimal
forge build
```

**Verification Points**:
- [ ] Builds without errors
- [ ] StandardSection only section generated
- [ ] Executable created

---

### Test 5.2: Full-Featured Project

**Purpose**: Test all features together.

```bash
forge create test_full

# Add package.toml with all features
cat > test_full/package.toml << 'EOF'
[project]
name = "test_full"
type = "executable"

[dependencies]
fmt = { git = "https://github.com/fmtlib/fmt.git", tag = "10.2.1" }
googletest = { git = "https://github.com/google/googletest.git", tag = "release-1.12.1", target = "GTest::gtest" }

[conan-dependencies]
spdlog = "1.12.0"
EOF

# Add test files
mkdir -p test_full/test
cat > test_full/test/main.cpp << 'EOF'
#include <gtest/gtest.h>
TEST(Basic, Pass) { EXPECT_TRUE(true); }
int main(int argc, char** argv) { testing::InitGoogleTest(&argc, argv); return RUN_ALL_TESTS(); }
EOF

# Add Lua custom CMake
mkdir -p test_full/.config/forge/build
cat > test_full/.config/forge/build/custom.lua << 'EOF'
forge.add_cmake([[
# Custom build step
message(STATUS "Custom CMake from Lua!")
]])
EOF

# Build
cd test_full
forge build
forge test
```

**Verification Points**:
- [ ] All sections generated
- [ ] Correct priority order
- [ ] Build succeeds
- [ ] Tests run

---

### Test 5.3: Library Project

**Purpose**: Test library project generation.

```bash
forge create test_library --type library
cd test_library
forge build
```

**Verification Points**:
- [ ] `add_library(NAME STATIC ...)` generated
- [ ] Install targets generated if `install_headers = true`

---

### Test 5.4: Resource Embedding Project

**Purpose**: Test resource embedding with modular CMake.

```bash
forge create test_resources
cd test_resources
echo "test" > assets/config.json
forge embed assets/config.json
forge build
```

**Verification Points**:
- [ ] Resources section generates correctly
- [ ] Embedded files accessible in code

---

## Troubleshooting Tests

| Issue | Test |
|-------|------|
| Sections not registering | Check CMakeRegistry.Initialize() called |
| Custom CMake not appearing | Check CustomCMakeSnippets not cleared prematurely |
| Priority wrong | Lower = earlier, verify with grep line numbers |
| Interface not found | Add `using forge.CMakeGeneration;` |

---

## Test Checklist

### Section Registration
- [ ] All 7 built-in sections register
- [ ] Each section has correct Name
- [ ] Sections stored in dictionary by name

### Priority Order
- [ ] StandardSection (1) appears first
- [ ] TestingSection (50) appears last
- [ ] Custom sections insert at correct position

### Lua Integration
- [ ] forge.add_cmake() function works
- [ ] Multiple snippets accumulate
- [ ] ${PROJECT_NAME} placeholder replaced
- [ ] CustomCMakeSection shows in correct position (45)

### CMake Generation
- [ ] StandardSection: C++ standard set correctly
- [ ] FetchContentSection: Git deps generate
- [ ] ProjectTargetSection: Executable/library correct
- [ ] LinkingSection: Dependencies linked
- [ ] TestingSection: Google Test configured
- [ ] ConanSection: find_package() generated

### End-to-End
- [ ] Minimal project builds
- [ ] Full-featured project builds and tests run
- [ ] Library project generates correctly
