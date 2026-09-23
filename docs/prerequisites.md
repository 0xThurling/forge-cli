# Prerequisites

Forge orchestrates the tools a C++ project already uses. Nothing here is
bundled with Forge, and nothing is installed silently — run `forge setup` to see
what your machine has, and `forge setup --install` to install what is missing.

## Required

| Tool | Version | Why |
|---|---|---|
| **C++ compiler** | C++20 | GCC, Clang or MSVC (`g++`/`clang++`/`cl`) |
| **CMake** | 3.23 or newer | Configures and builds the project (3.28+ for `modules = true`) |
| **Git** | any | Fetches dependencies and reads version information |

`forge setup` exits non-zero when one of these is missing, which makes it usable
as a CI gate: `forge setup || forge setup --install --yes`.

## Optional, per feature

| Tool | Needed for | Notes |
|---|---|---|
| **Ninja** | `build.modules = true` | CMake can only scan for C++20 modules with Ninja or Visual Studio 17.4+. Forge selects Ninja automatically when no generator is configured |
| **make** | the default generator | Ninja is preferred when present; one of the two is required |
| **ccache** / **sccache** | faster rebuilds | Detected on `PATH` and used as the compiler launcher; `build.compiler_launcher = "none"` opts out |
| **tar**, **unzip** | `forge extract`, `forge fetch` | Also needed by vcpkg's own bootstrap |
| **clang-format** | `forge format` | `CLANG_FORMAT` selects a specific binary |
| **clang-tidy** | `forge lint` | `CLANG_TIDY` selects a specific binary |
| **cpack** | `forge publish` | Ships with CMake, so it is usually already there |
| **python3** | the test suite's download scenarios | Only needed to run Forge's own end-to-end tests |
| **Conan 2.x** | `dependencies.conan` | `forge setup --install --tools conan` (package manager, or `pipx` where the archive has no Conan 2 / only 1.x) — see [Conan packages](dependency-management.md#conan-packages) |
| **vcpkg** | `dependencies.vcpkg` | `forge setup --install --tools vcpkg` — clones and bootstraps into `external/vcpkg`, where Forge finds it without any environment variable. See [Using vcpkg](dependency-management.md#using-vcpkg-manifest-mode) |
| **pkg-config** | `dependencies.pkgconfig` | Plus the `-dev`/`-devel` package of each module you use |

## Checking and installing

```bash
forge setup                      # table: tool, status, version, purpose
forge setup --install            # install what is missing (asks first)
forge setup --install --dry-run  # print the commands, change nothing
forge setup --tools cmake,ninja  # only these
forge setup --install --tools conan,vcpkg   # the ecosystem tools too
```

`--install` uses your machine's own package manager — apt-get, dnf, zypper, apk,
pacman, brew, winget or choco — prints the exact command, and lets the system's
`sudo` prompt for the password: Forge never handles it. Without a terminal
(for example in a script) it refuses to install unless `--yes` is passed.

`install.sh` runs `forge setup` after installing the binary, so a fresh install
tells you what is still missing.

## Environment check

`forge doctor` verifies the project environment: the configuration, the
directory layout, dependencies and conflicts, resources, scripts, features,
whether the generated files are ignored by git, and the toolchain (the same
table as `forge setup`, with versions). `forge doctor --fix` repairs what it
can — missing directories, missing `.gitignore` entries, and the Lua editor
stubs in `.config/forge/definitions/`.
