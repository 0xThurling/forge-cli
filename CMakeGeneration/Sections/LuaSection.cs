using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// A section registered from a Lua build script. Its priority was resolved
/// from the requested anchor when the registry applied it.
/// </summary>
public class LuaSection : CMakeSectionBase
{
  private readonly LuaCmakeSection _section;

  public LuaSection(LuaCmakeSection section, int priority)
  {
    _section = section;
    Priority = priority;
  }

  /// <summary>Namespaced, so a script cannot shadow a built-in section.</summary>
  public override string Name => "lua:" + _section.Name;

  public override int Priority { get; }

  public override string Generate(BuildContext context) =>
    $"# --- {_section.Name} (from Lua) ---\n" +
    _section.Content.Replace("${PROJECT_NAME}", context.Config.Project.Name);
}
