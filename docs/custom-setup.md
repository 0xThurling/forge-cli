# Custom Setup Scripts

Forge's dependency channels — Git, local `path`, and Conan — cover libraries
that already build with CMake. Some dependencies do not: they ship as an SDK
archive, need system packages installed, or expect a hand-written find module.
That is what **Lua build scripts** are for: the escape hatch of a meta-build
system.

## Where scripts live

```text
.config/forge/build/*.lua
```

`forge create` scaffolds the directory. Every `.lua` file in it runs, in
filename order, at the start of `forge build` — before CMake is generated — with
the full Lua API available:

| Function | Purpose |
|---|---|
| `forge.download(url, output, options?)` | download a file (`options`: `timeout`, `sha256`) |
| `forge.extract(archive, output, stripComponents?)` | unpack a `.zip` / `.tar` / `.tgz` / `.tar.gz` (`stripComponents` defaults to 0) |
| `forge.fetch(url, output?, stripComponents?)` | download **and** extract in one call (`stripComponents` defaults to 1, the GitHub-archive shape; output defaults to `external/<name>`) |
| `forge.pull_repo(url, tag?)` | `git clone` into `external/` |
| `forge.get_packages(password, manager, packages)` | install system packages |
| `forge.add_cmake(snippet, phase?)` | inject raw CMake |
| `forge.config.get(key)` / `forge.config.set(key, value)` | read/write config values |
| `forge.log.info(...)` / `warn(...)` / `error(...)` | log from a script |
| `forge.os`, `forge.distro`, `forge.package_manager` | branch on the machine (see [Lua Reference](lua-reference.md#environment-tables)) |

## The contract: `cmakeOptions`

A script tells CMake what it prepared by returning a `cmakeOptions` table.
Forge emits it **before the project target is created**, so variables,
packages and subdirectories are all in scope when the target is defined and
linked.

```lua
-- .config/forge/build/webgpu.lua
local sdk = "external/webgpu-sdk"

-- 1. prepare: fetch and unpack whatever the dependency needs
if not os.rename(sdk, sdk) then                      -- cheap "exists?" check
  forge.download("https://example.com/webgpu-sdk.zip", "webgpu-sdk.zip",
                 { sha256 = "<expected hash>" })
  forge.extract("webgpu-sdk.zip", sdk)
end

-- 2. declare: hand CMake the result
return {
  cmakeOptions = {
    variables         = { WEBGPU_DIR = sdk },         -- set(WEBGPU_DIR "...")
    findPackages      = { "Threads" },                -- find_package(... REQUIRED)
    includeDirs       = { "${WEBGPU_DIR}/include" },  -- include_directories(...)
    linkDirs          = { "${WEBGPU_DIR}/lib" },      -- link_directories(...)
    definitions       = { "WEBGPU_AVAILABLE=1" },     -- add_compile_definitions(...)
    compileOptions    = { "-DWEBGPU_STRICT=1" },      -- add_compile_options("...")
    linkLibraries     = { "webgpu" },                 -- link_libraries(...)
    addSubdirectories = { "external/dawn" }           -- add_subdirectory(...)
  }
}
```

| Key | Emitted as | Use for |
|---|---|---|
| `variables` | `set(NAME "value")` | SDK paths, feature switches |
| `findPackages` | `find_package(NAME REQUIRED)` | packages that ship a CMake config module |
| `includeDirs` / `linkDirs` | `include_directories` / `link_directories` | SDK layouts |
| `definitions` | `add_compile_definitions` | preprocessor switches |
| `compileOptions` | `add_compile_options` | flags the presets don't cover |
| `linkLibraries` | `link_libraries` | prebuilt SDK libraries |
| `addSubdirectories` | `add_subdirectory` | vendored sources that build with CMake |

Every key is optional; a single string is accepted wherever a list is expected.
Unknown keys produce a warning, so a typo is visible at build time.

## When the CMake doesn't fit the table

`forge.add_cmake(snippet, phase?)` injects raw CMake:

```lua
forge.add_cmake('set(MY_TOOLCHAIN_FLAG "1")', "pre")   -- before the target
forge.add_cmake('set_target_properties(my_app PROPERTIES ...)')  -- after it
```

- `"pre"` — before the project target is created: variables, `find_package`,
  `add_subdirectory`, toolchain settings.
- default (`"post"`) — after the target and its link line:
  `set_target_properties`, install rules, extra `target_link_libraries`.

`${PROJECT_NAME}` inside a snippet is replaced with the project name.

## System packages

```lua
forge.get_packages("nopass", "pacman", { "vulkan-headers", "vulkan-icd-loader" })
```

`manager` is one of `pacman`, `apt-get`, `dnf`, `brew`, `winget`, `choco`.
Pass `"nopass"` when sudo isn't needed or the credential is already cached;
otherwise the password is used to elevate. **Prefer `"nopass"`** — a password
written into `forge.lua` is a secret in version control.

## Worked example: WebGPU, GLFW and glfw3webgpu

A real setup script: three upstream projects that have no package, fetched at
configure time, built as subdirectories, and linked. Every primitive used here
is covered by the suite against a local server, so the recipe works offline once
the archives are in the cache.

```lua
-- .config/forge/build/webgpu.lua
forge.log.info("Setting up the WebGPU environment...")

-- System packages GLFW needs to build.
forge.get_packages("nopass", "pacman", { "xorg" })

-- forge.fetch downloads, extracts, and *returns* the extracted directory.
local webgpu = forge.fetch(
  "https://github.com/eliemichel/WebGPU-distribution/archive/refs/tags/main-v0.2.0.zip")
local glfw = forge.fetch(
  "https://eliemichel.github.io/LearnWebGPU/_downloads/6873a344e35ea9f5e4fc7e5cc85d3ab8/glfw-3.4.0-light.zip")
local glfw3webgpu = forge.fetch(
  "https://github.com/eliemichel/glfw3webgpu/releases/download/v1.2.0/glfw3webgpu-v1.2.0.zip")

-- A named section keeps its place in the generated file (and a later script can
-- replace it by name). `before:project_target` puts the subdirectories before
-- the target exists, so its link line can name them.
forge.add_section("webgpu-sources", "before:project_target", [[
add_subdirectory(]] .. webgpu .. [[)
add_subdirectory(]] .. glfw .. [[)
add_subdirectory(]] .. glfw3webgpu .. [[)
]])

-- Anchored *after* the test target (priority 50), so both targets exist by the
-- time this runs — which is what makes the guard below work.
forge.add_section("webgpu-link", "after:testing", [[
target_link_libraries(${PROJECT_NAME} PRIVATE webgpu glfw glfw3webgpu)
target_copy_webgpu_binaries(${PROJECT_NAME})

if(TARGET ${PROJECT_NAME}_tests)
  target_link_libraries(${PROJECT_NAME}_tests PRIVATE webgpu glfw glfw3webgpu)
endif()
]])

return {
  cmakeOptions = {
    -- GLFW's build options are cache variables: they have to be set *before*
    -- add_subdirectory, which is exactly what `cacheVariables` is for.
    cacheVariables = { GLFW_BUILD_WAYLAND = "OFF" },
  }
}
```

!!! tip "System packages differ by distribution"

    `xorg` is Arch's package group; Debian/Ubuntu call it `xorg-dev`. The list
    passed to `forge.get_packages` has to name what the machine's package
    manager actually provides.

Why it is shaped this way:

- **Two sections, two anchors.** `before:project_target` for the sources (the
  targets must exist before anything links them) and `after:testing` for the
  link line (the test target must exist before it can be linked). The
  [Lua reference](lua-reference.md#forgeadd_sectionname-position-content) lists
  every anchor and its priority.
- **`if(TARGET …)`.** With `testing = true` the test target links the same
  libraries; without tests the guard is simply false. (A directory-scoped
  `linkLibraries` would also reach the fetched projects themselves, which is why
  the link line is explicit.)
- **`${PROJECT_NAME}`** is substituted in every section, so the link line follows
  the project if you rename it.

!!! warning "`cacheVariables` must come before the subdirectory"

    GLFW reads `GLFW_BUILD_WAYLAND` when it is added, so a plain `set()` inside
    a snippet — which runs *after* `add_subdirectory` — would be ignored.
    `cacheVariables` is emitted before the project target, and so before the
    subdirectory.

!!! note "Fetches live in `external/`"

    Fetching at configure time needs the network once. Afterwards the archives
    live in `external/`, and `forge clean` does not remove them — so a rebuild
    is offline.

## Rules of thumb

- **Prefer a real channel.** If the dependency has a CMake build and a Git repo,
  a local checkout, or a Conan package, use `dependencies.direct` /
  `dependencies.conan` instead. Scripts are for what those cannot express.
- **Scripts run on every build.** Make them idempotent — check for the extracted
  SDK before downloading it again.
- **Keep them in the repo.** `.config/forge/build/` is part of the project, so
  the same script sets the dependency up on every machine.

## Named CMake sections

`forge.add_cmake` injects raw text at one of two fixed points. When the snippet
needs to sit somewhere specific — after the target, before the test block, next
to the packaging — use `forge.add_section` instead: it registers a *named*
section at an anchor and keeps its identity in the generated file.

```lua
forge.add_section("codegen", "after:project_target", [[
add_custom_command(OUTPUT ${PROJECT_NAME}_gen.cpp COMMAND my-codegen)
]])
```

The [Lua reference](lua-reference.md#forgeadd_sectionname-position-content) lists
every anchor and its priority.
