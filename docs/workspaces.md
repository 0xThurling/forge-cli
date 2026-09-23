# Workspaces

A workspace is a directory that contains several Forge projects. It exists so a
change that spans libraries and applications — a `fp` → `ml` → `app` chain, say —
can be built and tested in one command, in the right order.

## Layout

```text
my-stack/
├── fp/          forge.lua   (library)
├── ml/          forge.lua   (library, depends on fp)
├── app/         forge.lua   (executable, depends on ml)
└── forge.workspace.lua      (optional)
```

The workspace root is found by walking up from the current directory: the
nearest ancestor with a `forge.workspace.lua` wins, otherwise it is the
directory that contains the current project. Members are every subdirectory
holding a `forge.lua` (one level deeper is checked too, so `libs/foo/` works).

## The workspace file

`forge.workspace.lua` is optional. Without it, all discovered projects are
members; with it, you choose and order them explicitly:

```lua
return {
    name = "my-stack",
    projects = { "fp", "ml", "app" }   -- paths relative to the workspace root
}
```

## Dependency order

Order comes from the projects themselves, not from the file. A dependency
counts as local when its `path` resolves to another member, or when its name
matches a member (case-insensitive, as is the directory name). Dependencies are
built first; cycles keep the discovery order instead of failing.

```lua
-- ml/forge.lua
dependencies = {
    direct = {
        forgefp = { path = "../fp", target = "forgefp" }
    }
}
```

Set `target` when the dependency's CMake target is not the dependency key —
Forge links what you name, and the fetched project defines its own target.

## Commands

```bash
forge workspace list                  # projects, types, versions, dependencies
forge workspace build                 # build everything, in order
forge workspace build app             # app plus the projects it needs
forge workspace build --dry-run       # print the order, build nothing
forge workspace test                  # test everything, in order
forge workspace test ml --filter unit # extra flags go to each project
```

Each project is built by re-running `forge build` inside it, so output, flags,
presets and exit codes are exactly those of a normal build. The first failure
stops the run and names the project — later projects usually depend on the
failed one.

## Workspaces and `path` dependencies

A workspace does not rewrite your dependencies: `forge workspace build` simply
orders what your `forge.lua` files already declare. If you want the workspace's
sibling checkouts instead of a pinned git tag, either use `path` dependencies as
above, or run [`forge vendor`](cli-reference.md#vendor) to turn fetched git
dependencies into local copies for offline builds.
