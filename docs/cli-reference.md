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
Manages or displays project configuration.

**Usage:** `forge config [options]`

---

### `dependencies`
Lists or manages project dependencies.

**Usage:** `forge dependencies`

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

### `tree`
Displays the project directory structure.

**Usage:** `forge tree`

---

### `info`
Displays information about the current Forge project.

**Usage:** `forge info`

---

### `doctor`
Checks the environment for missing dependencies (CMake, Conan, etc.).

**Usage:** `forge doctor`
