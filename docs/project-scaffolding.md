# Project Scaffolding

Forge provides a set of commands to quickly generate boilerplate code and project structures, keeping your workflow efficient and consistent.

## Creating a New Project

The `create` command initializes a new project directory with a standard structure.

```bash
forge create <ProjectName> [--type <executable|library>]
```

### Directory Structure
When you create a project, Forge sets up the following:
- `src/`: Your C++ source files.
- `external/`: For Git-based dependencies.
- `assets/`: For resource files to be embedded.
- `.config/`: Forge internal configuration and cache.
- `forge.lua`: Your project configuration file.
- `.gitignore`: Pre-configured for Forge and C++ development.

---

## Generating Entities

The `new` command and its subcommands allow you to generate C++ entities inside the `src/` directory.

### New Class
Generates a header (`.h`) and a source (`.cpp`) file for a new class.

```bash
forge new class Player
```
- **Header**: Includes constructor/destructor declarations and a header guard.
- **Source**: Includes constructor/destructor implementations and the necessary include.

### New Struct
Generates a header file with a struct definition.

```bash
forge new struct Vector3
```

### New Header
Generates a blank header file with a header guard.

```bash
forge new header Utils
```

### New Source
Generates a blank C++ source file.

```bash
forge new source main
```

---

## Testing Scaffolding

If you enable testing in your `forge.lua` (`testing = true`), Forge can set up a GoogleTest environment for you.

When you run `forge test` for the first time in a project with testing enabled:
1. Forge creates a `test/` directory.
2. It generates a `test/main.cpp` with a sample test case.
3. It adds `googletest` to your `forge.lua` as a dependency.

You can then run your tests with:
```bash
forge test
```
