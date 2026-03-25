# Implementation Planning Memory

This file records detailed implementation knowledge for generating future implementation plans.
It is NOT in the docs submodule - it stays at the project root for AI reference.

---

## 1. Current Architecture Summary

### 1.1 CMake Generation: Registry + Priority Pattern

The CMake generation now uses a modular approach where each "section" is a separate class implementing `ICMakeSection`.

**Core Components:**
- `CMakeRegistry.cs` (59 lines) - Singleton managing all sections
- `ICMakeSection` interface - All sections implement this
- `CMakeSectionBase` abstract class - Base with common functionality

**Sections (by priority order):**
| Priority | Section | File | Lines | Purpose |
|----------|---------|------|-------|---------|
| 1 | StandardSection | StandardSection.cs | ~25 | CMAKE_CXX_STANDARD |
| 5 | ConanSection | ConanSection.cs | 25 | find_package calls |
| 10 | FetchContentSection | FetchContentSection.cs | ~45 | Git dependencies |
| 30 | ProjectTargetSection | ProjectTargetSection.cs | 35 | add_executable/library |
| 40 | LinkingSection | LinkingSection.cs | 33 | target_link_libraries |
| 45 | CustomCMakeSection | CustomCMakeSection.cs | 27 | Lua-injected CMake |
| 50 | TestingSection | TestingSection.cs | 35 | Google Test |

**Key Implementation Detail:** When adding a NEW section:
1. Create new class in `CMakeGeneration/Sections/`
2. Implement `ICMakeSection` or extend `CMakeSectionBase`
3. Override `Name`, `Priority`, `IsEnabled()`, `Generate()`
4. Register in `CMakeRegistry.Initialize()` (line 27-33)

### 1.2 Build Pipeline

**BuildCommand.Run()** (230 lines total):
1. Load config (line 64)
2. Run pre-build script (lines 72-80)
3. Run InstallCommand for Conan (lines 82-87)
4. Generate resource files if needed (lines 99-102)
5. **Run LuaBuilder** (line 105) - `LuaBuilder.RunBuilderScripts().RunSynchronously()`
6. **Generate CMake** (line 107) - `CMakeRegistry.Instance.Generate(projectConfig, Standard)`
7. Clear CustomCmakeSnippets (line 109)
8. Write root CMakeLists.txt (lines 111-120)
9. CMake configure (lines 122-153)
10. Create compile_commands.json symlink (lines 155-169)
11. CMake build (lines 171-200)
12. Run post-build script (lines 217-225)

### 1.3 Lua Integration

**LuaEngine.cs** (377 lines):
- `InitialiseLuaEngine()` (line 40) - Call at startup
- `SetDefinitionTables()` (line 72) - Register all Forge functions
- `SetCustomCMakeFunctions()` (lines 336-350) - Adds `forge.add_cmake()`

**Custom CMake Flow:**
```lua
-- In .config/forge/build/*.lua
forge.add_cmake([[
  message(STATUS "Hello from custom CMake!")
]])
```

1. Lua script calls `add_cmake("snippet")`
2. Adds to `ProjectBuildManager.CustomCmakeSnippets` (static list)
3. BuildCommand runs LuaBuilder BEFORE CMake generation (line 105)
4. CustomCMakeSection reads from the list (enabled when count != 0)
5. Snippets are cleared after generation (line 109)
6. Placeholder `${PROJECT_NAME}` is replaced with actual project name

---

## 2. File Locations and Important Paths

| Item | Path | Used In |
|------|------|---------|
| Config file | `package.toml` | ProjectConfigManager |
| Lua build scripts | `.config/forge/build/*.lua` | LuaBuilder |
| Generated CMake | `.config/cmake/CMakeLists.txt` | BuildCommand (line 118) |
| Root CMakeLists.txt | `CMakeLists.txt` | BuildCommand (line 120) |
| Lua definitions | `.config/forge/definitions/forge.lua` | LuaEngine |
| Resource files | `src/resources/` | Utils.GenerateResourceFiles |

---

## 3. Key Classes and Their Responsibilities

### ProjectBuildManager.cs
- `CustomCmakeSnippets` - Static list for Lua-injected CMake
- `FindDependencies` - List of Conan packages
- `LinkDependencies` - List of libraries to link

### ProjectConfigManager.cs
- `LoadConfig()` - Parses package.toml into ProjectConfig
- `SaveConfig()` - Writes ProjectConfig back to TOML
- Uses `Tommy` library for TOML parsing

### LuaBuilder.cs
- `RunBuilderScripts()` - Executes all `.lua` files in `.config/forge/build/`
- Returns Task (must call .RunSynchronously() or await)

### Utils.cs
- `GenerateResourceFiles(files)` - Creates embedded_resources.h/cpp

---

## 4. Common Code Patterns

### Adding a New Command
```csharp
[CliCommand(Name = "command-name", Description = "...", Parent = typeof(ParentCommand))]
public class CommandName
{
    [CliOption(Description = "...")]
    public bool Option { get; set; }

    public int Run()
    {
        // Implementation
        return 0;
    }
}
```

### Adding a CMake Section
```csharp
public class MySection : CMakeSectionBase
{
    public override string Name => "my_section";
    public override int Priority => 35; // Between 30 and 40
    
    public override bool IsEnabled(ProjectConfig config) => /* condition */;
    
    public override string Generate(ProjectConfig config)
    {
        return "# --- My Section ---\n...";
    }
}
```
Then register in `CMakeRegistry.Initialize()`.

### Adding a Lua Function
```csharp
private static void SetMyFunction()
{
    var myFunc = new LuaFunction("my_func", (context, token) =>
    {
        var arg = context.GetArgument<string>(0);
        // Implementation
        return ValueTask.FromResult(0);
    });
    _cpm[new LuaValue("my_func")] = new LuaValue(myFunc);
}
```
Then call in `SetDefinitionTables()`.

---

## 5. External Dependencies

| Tool | Purpose | Called From |
|------|---------|-------------|
| cmake | Build configuration | BuildCommand.cs |
| conan | Package management | InstallCommand |
| git | Clone repositories | LuaEngine (pull_repo) |
| dotnet | Compilation | - |

---

## 6. Current Limitations / Gotchas

1. **Async/Sync Mismatch**: LuaBuilder returns Task but BuildCommand.Run() is sync - use `.RunSynchronously()`
2. **Snippet Clearing**: Must clear `CustomCmakeSnippets` after build or they'll persist across builds
3. **Priority Collision**: Sections with same priority will override each other (dict behavior)
4. **Lua Only Runs Once**: LuaBuilder runs at build time, not for every command
5. **Test Directory**: TestingSection only enables if `Directory.Exists("test")` AND has googletest dependency

---

## 7. Configuration Schema (package.toml)

```toml
[project]
name = "myproject"
type = "executable"  # or "library"
install_headers = false  # for libraries

[dependencies]
googletest = { git = "...", tag = "..." }

[resources]
files = ["assets/*"]

[scripts]
pre-build = "scripts/pre.sh"
post-build = "scripts/post.sh"
```

---

## 8. Version Information

- **.NET**: 10.0
- **Target Framework**: net10.0
- **CLI Framework**: DotMake.CommandLine 2.0.0
- **TOML**: Tommy 3.1.2
- **Console UI**: Spectre.Console 0.50.0
- **Lua**: LuaCSharp 0.5.0

---

## 9. Implementation Plan Template

When creating new implementation plans, include:

1. **Overview** - What feature, estimated time, difficulty
2. **Prerequisites** - What code to understand first
3. **Step-by-step** - Numbered steps with code snippets
4. **Files to Create/Modify** - Table listing new and modified files
5. **Testing** - How to verify the implementation works
6. **Troubleshooting** - Common issues and solutions

Include these details for EACH step:
- File path and approximate line numbers
- Exact code to add (use code blocks)
- Purpose/why this change is needed
- Any dependencies on other steps

---

## 10. Recent Changes Log

### Latest: CMake Registry Pattern Implementation
- Added CMakeRegistry singleton with section registration
- Created 7 built-in sections with priority ordering
- Added `forge.add_cmake()` Lua function for custom CMake injection
- Updated BuildCommand to use CMakeRegistry
- Added compile_commands.json symlink creation for LSP support

### Previous: Lua Engine Setup
- Initial Lua sandbox with environment functions
- `forge.pull_repo()`, `forge.get_packages()`, `forge.log.info()`
- OS/distro/package_manager detection

---

*Last updated: 2026-03-25*
