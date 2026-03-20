# Forge Architecture Documentation

## Overview

Forge is a C++ project manager built with C# (.NET 10) that provides a streamlined CLI for managing C++ projects using CMake. The architecture follows a modular, command-based design pattern using the DotMake.CommandLine library.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Program.cs                                │
│                   (Entry Point & Bootstrap)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    LuaEngine (Initialisation)                   │
│              - Sets up Lua state                                 │
│              - Registers environment functions                  │
│              - Loads definitions                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    CLI Commands Layer                            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│  │  create  │ │  build   │ │   run    │ │   test   │   ...     │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Core Services Layer                          │
│  ┌──────────────────┐  ┌──────────────────┐                   │
│  │ProjectConfigManager│  │ProjectBuildManager│                  │
│  └──────────────────┘  └──────────────────┘                   │
│  ┌──────────────────┐  ┌──────────────────┐                   │
│  │     Utils        │  │   LuaBuilder     │                   │
│  └──────────────────┘  └──────────────────┘                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Models Layer                                 │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐   │
│  │ProjectConfig│ │ ProjectSection│ │ Dependency │ │Resources │   │
│  └────────────┘ └────────────┘ └────────────┘ └────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    External Systems                             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│  │  CMake  │ │  Conan   │ │   Git    │ │  Lua     │           │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘           │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Entry Point (Program.cs)

The application starts by:
1. Initializing the Lua sandbox engine
2. Running builder scripts from `.config/forge/build/`

**Key responsibilities:**
- Bootstrap the Lua environment
- Execute pre-configured build scripts

### 2. CLI Commands Layer

Commands are built using **DotMake.CommandLine**, a convention-based CLI framework for .NET.

#### Command Hierarchy

```
RootCommand (forge)
├── create <name>         - Create new C++ project
├── build                 - Generate CMake and build
├── run [script]          - Run project or custom scripts
├── test                  - Build and run tests
├── clean                 - Remove build directory
├── embed <file>          - Embed resource file
├── new
│   ├── class <name>      - Generate class boilerplate
│   ├── struct <name>    - Generate struct boilerplate
│   ├── header <name>    - Generate header file
│   └── source <name>    - Generate header+source pair
├── project
│   ├── info              - Display project info
│   ├── tree              - Display project tree
│   ├── stats             - Display project statistics
│   ├── dependencies      - List dependencies
│   └── scripts           - List available scripts
└── install               - Install Conan dependencies
```

#### Command Implementation Pattern

All commands follow a consistent pattern:

```csharp
[CliCommand(Name = "command", Description = "...", Parent = typeof(ParentCommand))]
public class CommandName
{
    [CliArgument]           // Positional arguments
    public string Argument { get; set; }

    [CliOption]             // Optional flags
    public bool Option { get; set; }

    public int Run()        // Main execution logic
    {
        // Implementation
        return 0;           // 0 = success, non-zero = error
    }
}
```

### 3. Core Services

#### ProjectConfigManager

Manages the `package.toml` configuration file using the **Tommy** TOML library.

**Key methods:**
- `LoadConfig()` - Parse and load package.toml
- `SaveConfig()` - Serialize config back to TOML
- `FindProjectRoot()` - Locate project root by walking up directories

**Configuration structure:**
```toml
[project]
name = "my_project"
type = "executable"
install_headers = false

[dependencies]
# Git-based dependencies

[conan-dependencies]
# Conan packages

[resources]
files = ["assets/*"]

[scripts]
pre-build = "echo before build"
post-build = "echo after build"
```

#### ProjectBuildManager

Static state manager for build-time information.

**Managed state:**
- `LinkDependencies` - CMake target link libraries from Conan
- `FindDependencies` - CMake find_package modules from Conan

This component bridges the Conan install phase with the build phase.

#### Utils

Provides utility functions:

- `GenerateResourceFiles()` - Converts binary files to C++ header/source
- `CreateTests()` - Sets up Google Test framework
- `SanitizeFileName()` - Converts filenames to valid C++ identifiers

### 4. Lua Engine

The Lua engine provides a **scriptable build system** allowing users to customize the build process.

#### LuaEngine (Commands/Lua/LuaEngine.cs)

Initializes a Lua state with sandboxed environment functions.

**Available Lua globals:**

```lua
-- Operating System
forge.os.current        -- "linux", "macos", "windows"
forge.os.windows
forge.os.macos
forge.os.linux

-- Distribution (Linux)
forge.distro.ubuntu
forge.distro.arch
forge.distro.fedora
-- ... etc

-- Package Managers
forge.package_manager.brew
forge.package_manager.aptget
forge.package_manager.pacman
-- ... etc

-- Functions
forge.pull_repo(url)                    -- Clone git repository
forge.get_packages(password, manager, packages)
forge.log.info(message)                 -- Logging

-- Environment
forge.current_working_dir               -- Current directory
```

#### LuaBuilder

Executes Lua scripts from `.config/forge/build/` directory during the build process.

#### LuaDefinitionGenerator

Generates environment definitions for new projects.

### 5. Models

Plain C# classes representing configuration data.

```csharp
public class ProjectConfig
{
    public ProjectSection Project { get; set; }
    public Dictionary<string, Dependency> Dependencies { get; set; }
    public Dictionary<string, string> ConanDependencies { get; }
    public ResourcesSection Resources { get; set; }
    public Dictionary<string, string> Scripts { get; }
}

public class Dependency
{
    public string Git { get; set; }      // Git repository URL
    public string Tag { get; set; }        // Git tag/branch
    public string Target { get; set; }    // CMake target name
}

public class ResourcesSection
{
    public List<string> Files { get; set; }
}
```

## Build Pipeline

### Full Build Flow

```
1. User runs: forge build
   │
   ▼
2. Load package.toml
   │
   ▼
3. Run pre-build script (if defined)
   │
   ▼
4. Install Conan dependencies
   │
   ├─ Generate conanfile.txt
   ├─ Run conan install
   └─ Parse output for CMake targets
   │
   ▼
5. Generate resource files (if any)
   │
   ▼
6. Generate CMakeLists.txt
   │
   ├─ Set C++ standard
   ├─ Configure FetchContent dependencies
   ├─ Add project target (executable/library)
   ├─ Link dependencies
   └─ Configure Google Test (if test/ exists)
   │
   ▼
7. CMake configure
   │
   ▼
8. CMake build
   │
   ▼
9. Create compile_commands.json symlink
   │
   ▼
10. Run post-build script (if defined)
```

### CMake Generation

The build command generates two CMake files:

1. **Root CMakeLists.txt** - Minimal, includes generated config
2. **.config/cmake/CMakeLists.txt** - Auto-generated with:
   - C++ standard configuration
   - FetchContent dependencies
   - Project target definition
   - Test configuration (if applicable)

## Data Flow

### Configuration Loading

```
package.toml
    │
    ▼ (Tommy parser)
TomlTable
    │
    ▼ (Manual mapping)
ProjectConfig (C# model)
    │
    ▼
CLI Commands use ProjectConfig
```

### Resource Embedding

```
Binary file (e.g., shader.glsl)
    │
    ▼ (Utils.GenerateResourceFiles)
    │
    ├─ Generate embedded_resources.h
    │  └── Function declarations
    │
    └─ Generate embedded_resources.cpp
        └── Byte arrays + lookup map
```

## Extension Points

### Custom Commands

Place Lua scripts in:
```
.config/forge/commands/     # Future: custom CLI commands
.config/forge/build/        # Build-time scripts
.config/forge/templates/    # Code generation templates
```

### Environment Definitions

The Lua environment is extensible. New functions can be added in `LuaEngine.cs` by:
1. Creating a new `LuaFunction`
2. Adding it to the `_cpm` table

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| DotMake.CommandLine | 2.0.0 | CLI framework |
| Tommy | 3.1.2 | TOML parsing |
| Spectre.Console | 0.50.0 | Terminal UI |
| LuaCSharp | 0.5.0 | Lua sandbox |

## Platform Support

- Windows x64
- Linux x64
- macOS x64/arm64

The platform detection is handled in `LuaEngine.GetOperatingSystem()` and `GetLinuxDistro()`.
