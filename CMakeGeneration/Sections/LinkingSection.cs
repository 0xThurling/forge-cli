using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class LinkingSection : CMakeSectionBase
{
  public override string Name => "linking";

  public override int Priority => 40;

  public override string Generate(ProjectConfig config)
  {
    var linkTargets = new List<string>();

    foreach (var dep in config.Dependencies)
    {
      if (dep.Key != "googletest")
      {
        var target = string.IsNullOrEmpty(dep.Value.Target) ? dep.Key : dep.Value.Target;
        linkTargets.Add(target);
      }
    }

    // From Conan
    linkTargets.AddRange(ProjectBuildManager.LinkDependencies);

    if (linkTargets.Count == 0) return string.Empty;

    return $@"
# --- Linking ---
target_link_libraries({config.Project.Name} PRIVATE {string.Join(" ", linkTargets)})
      ";
  }
}
