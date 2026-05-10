# CLI Reference

Forge provides a set of commands to manage your C++ project's lifecycle.

## General Usage

```bash
forge [command] [options] [arguments]
```

---

## Commands

### `create`
Creates a new Forge project.

**Usage:** `forge create <Name> [options]`

- `<Name>`: The name of the project.
- `--type <type>`: Type of project to create (`executable` or `library`). Defaults to `executable`.

**Example:**
```bash
forge create MyProject --type library
```

---

### `build`
Generates CMake files and builds the project.

**Usage:** `forge build [options]`

- `--verbose`: Show verbose output from CMake.
- `--standard <version>`: C++ standard to use (11, 14, 17, 20). Defaults to 20.

**Example:**
```bash
forge build --verbose --standard 17
```

---

### `run`
Runs a custom script defined in `forge.lua` or the main executable if no script is specified.

**Usage:** `forge run [ScriptName]`

- `[ScriptName]`: Optional name of the script to run (from the `scripts` section in `forge.lua`).

**Example:**
```bash
forge run
forge run test
```

---

### `clean`
Cleans the build directory.

**Usage:** `forge clean`

---

### `config`
Manages project configuration.

#### `config migrate`
Migrates an old `package.toml` configuration file to the new `forge.lua` format.

**Usage:** `forge config migrate`

---

### `project`
Parent command for project information and management.

#### `project info`
Displays a summary of the current project's configuration.

**Usage:** `forge project info`

#### `project tree`
Displays the project directory structure.

**Usage:** `forge project tree`

#### `project dependencies`
Lists all project dependencies defined in `forge.lua`.

**Usage:** `forge project dependencies`

#### `project scripts`
Lists all custom scripts defined in `forge.lua`.

**Usage:** `forge project scripts`

#### `project stats`
Displays statistics about the project, including file counts and total lines of code.

**Usage:** `forge project stats`

---

### `download`
Downloads and prepares direct Git dependencies defined in `forge.lua`.

**Usage:** `forge download`

---

### `embed`
Manages resource embedding.

**Usage:** `forge embed`

---

### `new`
Parent command for creating new C++ entities.

#### `new class <Name>`
Generates a new C++ class (Header + Source).
**Example:** `forge new class Player`

#### `new struct <Name>`
Generates a new C++ struct.
**Example:** `forge new struct Vector3`

#### `new header <Name>`
Generates a new C++ header file.
**Example:** `forge new header Utils`

#### `new source <Name>`
Generates a new C++ source file.
**Example:** `forge new source main`

---

### `test`
Runs project tests (if enabled).

**Usage:** `forge test`

---

### `doctor`
Checks the environment for missing dependencies (CMake, Conan, etc.).

**Usage:** `forge doctor`
