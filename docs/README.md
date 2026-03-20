# Forge Documentation

Welcome to the Forge project documentation. This directory contains comprehensive documentation about Forge's architecture, functionality, and codebase.

## Documentation Structure

```
docs/
├── ARCHITECTURE.md    # Technical architecture and design
├── FUNCTIONALITY.md    # Complete command reference
├── AI_MEMORY.md       # Quick reference for AI models
└── README.md          # This file
```

## Contents

### [ARCHITECTURE.md](ARCHITECTURE.md)

Detailed technical documentation covering:

- **System Architecture**: High-level component diagrams (Mermaid)
- **Core Components**: In-depth explanation of each layer
  - Entry Point (Program.cs)
  - CLI Commands Layer
  - Core Services (ProjectConfigManager, ProjectBuildManager, Utils)
  - Lua Engine
  - Models Layer
- **Build Pipeline**: Complete build flow with diagrams
- **Data Flow**: Configuration loading and resource embedding flows
- **Extension Points**: How to extend Forge
- **Dependency Analysis**: External dependencies
- **Platform Support**: Cross-platform implementation details

### [FUNCTIONALITY.md](FUNCTIONALITY.md)

Complete command reference including:

- **Command Reference**: All CLI commands with examples
  - Project lifecycle (create, build, run, test, clean)
  - Code generation (new class/struct/header/source)
  - Resource management (embed)
  - Project information (info, tree, stats, dependencies, scripts)
  - Package management (install/Conan)
- **Configuration Guide**: Complete package.toml reference
- **Resource Management**: How to embed and use resources
- **Lua Scripting**: Build scripts and API reference
- **Testing**: Google Test integration
- **Editor Integration**: LSP/compile_commands.json setup
- **Best Practices**: Project structure and patterns
- **Troubleshooting**: Common issues and solutions

### [AI_MEMORY.md](AI_MEMORY.md)

Quick reference for AI models working with the codebase:

- **Project Metadata**: Quick facts about the project
- **Directory Structure**: Visual graph of file organization
- **Key Files**: Purpose of each important file
- **Command Pattern**: How to add new commands
- **Lua API**: Quick reference for available functions
- **Common Modifications**: Typical extension points
- **Build Commands**: How to build and test
- **Constants**: Important values and locations

## Diagram Support

This documentation uses **Mermaid** diagrams for visual representations. Many markdown editors and platforms (GitHub, GitLab, VS Code with extension) render these automatically.

Example diagram types used:
- Flowcharts (`flowchart TD`)
- Sequence diagrams (`sequenceDiagram`)
- Class diagrams (`classDiagram`)

## Additional Resources

- [Main README](../README.md) - Project overview and installation
- [GitHub Repository](https://github.com/0xThurling/forge)

## Quick Links

| Topic | Documentation |
|-------|---------------|
| How to create a project | [FUNCTIONALITY.md#forge-create](FUNCTIONALITY.md#forge-create) |
| How to configure dependencies | [FUNCTIONALITY.md#configuration-guide](FUNCTIONALITY.md#configuration-guide) |
| How to add Lua scripting | [FUNCTIONALITY.md#lua-scripting](FUNCTIONALITY.md#lua-scripting) |
| Understanding the architecture | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Modifying the codebase | [AI_MEMORY.md#common-modification-points](AI_MEMORY.md#common-modification-points) |
