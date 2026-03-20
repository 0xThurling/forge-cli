# AI Memory - Forge Codebase

## Project Overview

**Forge** is a C++ project manager CLI tool built with C# (.NET 10). It streamlines C++ development by providing project scaffolding, CMake integration, dependency management, and code generation.

## Quick Facts

- **Language:** C# (.NET 10)
- **CLI Framework:** DotMake.CommandLine 2.0.0
- **Build Target:** AOT-compiled executable
- **Runtime:** Cross-platform (Windows, Linux, macOS)

## Key Technologies

| Package | Version | Purpose |
|---------|---------|---------|
| DotMake.CommandLine | 2.0.0 | CLI command framework |
| Tommy | 3.1.2 | TOML config file parsing |
| Spectre.Console | 0.50.0 | Terminal UI styling |
| LuaCSharp | 0.5.0 | Lua scripting sandbox |

## Directory Structure

```
/root/forge/
├── Program.cs                    # Entry point, initializes Lua
├── ProjectConfigManager.cs       # package.toml read/write
├── ProjectBuildManager.cs        # Build state (link/find deps)
├── Utils.cs                      # Resource generation, test setup
├── Commands/                    # All CLI commands
│   ├── RootCommand.cs           # Base command
│   ├── CreateCommand.cs          # New project
│   ├── BuildCommand.cs           # CMake build
│   ├── RunCommand.cs             # Run project/scripts
│   ├── TestCommand.cs            # Run tests
│   ├── CleanCommand.cs           # Clean build
│   ├── EmbedCommand.cs           # Embed resources
│   ├── NewCommand.cs             # Code gen parent
│   │   ├── NewClassCommand.cs
│   │   ├── NewStructCommand.cs
│   │   ├── NewHeaderCommand.cs
│   │   └── NewSourceCommand.cs
│   ├── ProjectCommand.cs         # Project info parent
│   │   ├── InfoCommand.cs
│   │   ├── TreeCommand.cs
│   │   ├── StatsCommand.cs
│   │   ├── DependenciesCommand.cs
│   │   └── ScriptsCommand.cs
│   ├── Conan/
│   │   └── InstallCommand.cs     # Conan integration
│   └── Lua/
│       ├── LuaEngine.cs          # Lua sandbox setup
│       ├── LuaBuilder.cs         # Runs Lua build scripts
│       └── LuaDefinitionGenerator.cs
├── Models/
│   ├── ProjectConfig.cs          # Main config model
│   ├── ProjectSection.cs         # [project] section
│   ├── Dependency.cs             # Git dependency model
│   └── ResourcesSection.cs       # Resource files model
└── docs/
    ├── ARCHITECTURE.md
    ├── FUNCTIONALITY.md
    └── AI_MEMORY.md              # This file
```

## Important Code Patterns

### Command Definition Pattern

```csharp
[CliCommand(Name = "command", Description = "...", Parent = typeof(ParentCommand))]
public class MyCommand
{
    [CliArgument]           // Positional arg: forge command <arg>
    public string Argument { get; set; }

    [CliOption]             // Optional flag: forge command --option
    public bool Option { get; set; }

    public int Run()        // Returns 0 for success
    {
        // Implementation
        return 0;
    }
}
```

### Configuration Loading

```csharp
var config = ProjectConfigManager.LoadConfig();
// Returns ProjectConfig or null if no package.toml

// Config properties:
config.Project.Name           // Project name
config.Project.Type           // "executable" or "library"
config.Dependencies           // Dict<string, Dependency>
config.ConanDependencies      // Dict<string, string>
config.Resources.Files        // List<string>
config.Scripts                // Dict<string, string>
```

### Lua Environment Setup

Lua functions are registered in `LuaEngine.SetDefinitionTables()`:
- `forge.pull_repo(url)` - Clone git repo
- `forge.get_packages(pass, manager, packages)` - Install system packages
- `forge.log.info(msg)` - Logging
- `forge.os.current` - "linux", "macos", "windows"
- `forge.distro.*` - Linux distribution constants

## Key Implementation Details

### CMake Generation (BuildCommand.cs:60-183)

- Generates `.config/cmake/CMakeLists.txt` with all project config
- Root `CMakeLists.txt` is minimal and includes the generated file
- Uses FetchContent for Git dependencies
- Uses find_package for Conan dependencies

### Resource Embedding (Utils.cs:9-106)

- Reads binary files and generates C++ byte arrays
- Creates `embedded_resources.h` and `embedded_resources.cpp`
- Provides `Embedded::get("filename")` API

### Project Root Detection

`ProjectConfigManager.FindProjectRoot()` walks up directory tree looking for `package.toml`.

### Lua Scripts Location

Scripts in `.config/forge/build/*.lua` are executed by `LuaBuilder.RunBuilderScripts()` during startup.

## Common Modification Points

1. **Add new command:** Create class in Commands/ with `[CliCommand]` attribute
2. **Add config option:** Add to ProjectConfig and update ProjectConfigManager
3. **Add Lua function:** Add in LuaEngine.cs `SetDefinitionTables()`
4. **Modify CMake generation:** Edit BuildCommand.cs cmake generation section

## Testing

```bash
# Build the tool
dotnet build -c Release

# Run locally
dotnet run --project . -- [command]

# Publish for current platform
dotnet publish -c Release
```

## External Dependencies Called

- `cmake` - Build system
- `conan` - Package manager
- `git` - Repository operations
- `bash` - Script execution
