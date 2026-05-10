# Lua API Reference

Forge provides a powerful Lua API that allows you to script your build process, manage external resources, and interact with the project configuration. These functions are available under the global `forge` table.

## Logging

### `forge.log.info(message)`
Logs an informational message to the console.
- `message`: The string to log.

### `forge.log.warn(message)`
Logs a warning message to the console.

### `forge.log.error(message)`
Logs an error message to the console.

---

## Configuration

### `forge.config.get(key)`
Retrieves a value from the `custom` section of `forge.lua`.
- `key`: The key to retrieve.
- **Returns**: The value as a string, or `nil` if not found.

### `forge.config.has_feature(name)`
Checks if a feature is enabled in the `features` section.
- `name`: The name of the feature.
- **Returns**: Boolean.

### `forge.config.get_feature_option(feature, option, default)`
Retrieves an option from a specific feature.
- `feature`: The feature name.
- `option`: The option key.
- `default`: The default value to return if not found.

---

## External Resources & Git

### `forge.pull_repo(url, tag?)`
Clones a Git repository into the `external/` directory.
- `url`: The Git repository URL.
- `tag`: (Optional) The branch, tag, or commit hash to clone.

### `forge.fetch(url, output_dir?)`
Downloads a file (usually a zip) and extracts it.
- `url`: The URL to download from.
- `output_dir`: (Optional) The directory to extract to. Defaults to `external/<archive_name>`.
- **Returns**: The path to the extracted directory.

### `forge.download(url, output, options?, progress_callback?)`
Downloads a file to a specific location.
- `url`: The URL to download.
- `output`: The destination file path.
- `options`: (Optional) A table like `{ timeout = 300 }`.
- `progress_callback`: (Optional) A function receiving `(bytes_downloaded, total_bytes)`.

### `forge.extract(archive_path, output_dir, strip_components?)`
Extracts a ZIP or TAR archive.
- `archive_path`: Path to the archive.
- `output_dir`: Destination directory.
- `strip_components`: (Optional) Number of leading components to strip from file names.

---

## Build System Integration

### `forge.add_cmake(snippet)`
Adds a custom CMake snippet directly into the generated `CMakeLists.txt`.
- `snippet`: The CMake code to inject.

### `forge.get_packages(password, manager, packages)`
Installs system packages using a package manager (e.g., `apt`, `pacman`).
- `password`: The sudo password (use `"nopass"` if not required).
- `manager`: The package manager name.
- `packages`: A Lua table (list) of package names.
