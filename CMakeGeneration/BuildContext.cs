using forge.Models;

namespace forge.CMakeGeneration;

/// <summary>
/// Everything a single build accumulates besides the project configuration:
/// what the Lua build scripts contributed, what Conan reported, and the
/// dependency names the generated CMake has to link.
/// </summary>
/// <remarks>
/// One context per build, passed to every CMake section. Contributions are
/// therefore scoped to the build that made them: a second build in the same
/// process (a workspace run, or <c>forge test</c> after <c>forge build</c>)
/// starts from an empty context instead of inheriting the previous one.
/// </remarks>
public class BuildContext
{
  /// <summary>
  /// The project configuration. Replaced when a build reloads it (for example
  /// after injecting the test framework), so sections always see the current one.
  /// </summary>
  public ProjectConfig Config { get; set; } = new();

  /// <summary>
  /// CMake targets reported by Conan, passed to <c>target_link_libraries</c>
  /// (e.g. <c>fmt::fmt</c>).
  /// </summary>
  public List<string> LinkDependencies { get; } = [];

  /// <summary>
  /// Packages reported by Conan, emitted as <c>find_package</c> (e.g. <c>fmt</c>).
  /// </summary>
  public List<string> FindDependencies { get; } = [];

  /// <summary>
  /// Snippets injected after the project target and its link line
  /// (<c>forge.add_cmake(snippet)</c>).
  /// </summary>
  public List<string> CustomCmakeSnippets { get; } = [];

  /// <summary>
  /// Snippets injected before the project target is created
  /// (<c>forge.add_cmake(snippet, "pre")</c>), for toolchain and SDK setup.
  /// </summary>
  public List<string> CustomCmakeSnippetsPre { get; } = [];

  /// <summary>
  /// Structured CMake options returned by Lua build scripts
  /// (<c>.config/forge/build/*.lua</c> → <c>cmakeOptions</c>).
  /// </summary>
  public CmakeOptions LuaOptions { get; } = new();

  /// <summary>
  /// Named CMake sections registered from Lua
  /// (<c>forge.add_section(name, position, content)</c>), in registration
  /// order. A repeated name replaces the earlier registration.
  /// </summary>
  public List<LuaCmakeSection> LuaSections { get; } = [];

  /// <summary>Registers a Lua section, replacing one with the same name.</summary>
  public void RegisterLuaSection(LuaCmakeSection section)
  {
    LuaSections.RemoveAll(existing => existing.Name == section.Name);
    LuaSections.Add(section);
  }
}
