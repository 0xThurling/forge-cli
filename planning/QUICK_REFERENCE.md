# Quick Reference Guide

## Priority Order

Lower numbers = appear earlier in generated CMake file.

| Priority | Section | Purpose |
|----------|---------|---------|
| 0 | FormatSection | Code formatting |
| 1 | StandardSection | C++ standard config |
| 5 | ConanSection | Conan packages |
| 10 | FetchContentSection | Git dependencies |
| 30 | ProjectTargetSection | add_executable/library |
| 40 | LinkingSection | target_link_libraries |
| 45 | CustomCMakeSection | From Lua scripts |
| 50 | TestingSection | Google Test |
| 100+ | CoverageSection | Code coverage |
| 200 | DoxygenSection | Documentation |

## Pattern for New Section

```csharp
namespace forge.CMakeGeneration
{
    public class MyNewSection : CMakeSectionBase
    {
        public override string Name => "my_section";
        public override int Priority => 35; // Choose position
        
        public override bool IsEnabled(ProjectConfig config)
            => config.Features.ContainsKey("my_feature"); // Conditional
        
        public override string Generate(ProjectConfig config)
        {
            return @"# --- My Section ---";
        }
    }
}
```

## Registering a Section

In `CMakeRegistry.Initialize()`:

```csharp
Register(new MyNewSection());
```

## Lua Functions Available

```lua
-- Logging
forge.log.info("message")

-- Git
forge.pull_repo("https://github.com/user/repo.git")

-- System packages
forge.get_packages("nopass", forge.package_manager.brew, {"package"})

-- Custom CMake (key feature!)
forge.add_cmake("message(STATUS 'Hello')")

-- Environment
forge.os.current      -- "linux", "macos", "windows"
forge.distro.my_distro -- detected Linux distro
forge.current_working_dir
```

## CMake Placeholders

In Lua `forge.add_cmake()`:

| Placeholder | Replaced With |
|-------------|---------------|
| `${PROJECT_NAME}` | Actual project name |

---

## Testing Checklist

- [ ] Build succeeds
- [ ] Default project builds without Lua scripts
- [ ] Custom CMake appears in generated file
- [ ] `${PROJECT_NAME}` replaced correctly
- [ ] Multiple custom CMake snippets work
- [ ] Custom CMake clears after build
