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


## Templates

`forge new class/struct/header/source` uses the built-in scaffolding unless the
project provides a template. Drop a file in `.config/forge/templates/` named
after the kind — `class.hpp`, `struct.h`, `source.cpp`, `header.h` — and it is
used instead, with `{{name}}` and `{{NAME}}` substituted:

```cpp
// .config/forge/templates/class.hpp
#pragma once
namespace demo {
class {{name}} {  // {{NAME}} in caps
 public:
  {{name}}();
};
}
```

Both `.h` and `.hpp` are accepted for headers. Without a template the built-in
file is written, so adding one is a per-project opt-in.

## Project commands

Shell scripts in `.config/forge/commands/` behave like `scripts` entries:

```bash
.config/forge/commands/format.sh   #  ->  forge run format
```

`forge project scripts` lists them (marked with their directory), `forge run
<name>` executes them with `bash`, and their exit codes propagate. A script of
the same name declared in `forge.lua` takes precedence.

## After scaffolding

Generated sources are starting points, not formatted output: run
`forge format` before the first commit (and `forge format --check` in CI) so the
new files match the project's `.clang-format`. `forge doctor` re-checks the
layout if you moved things around.
