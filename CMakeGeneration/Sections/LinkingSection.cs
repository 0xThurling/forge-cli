using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class LinkingSection : CMakeSectionBase
{
  public override string Name => "linking";

  public override int Priority => 40;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var linkTargets = LinkTargets.For(config, context);

    // The project's own extra libraries are part of it, so its targets link
    // them: an application plus the library it is built from.
    foreach (var target in config.Targets.Where(target => target.Type == "library"))
      linkTargets.Add(target.Name);

    if (linkTargets.Count == 0) return string.Empty;

    return $@"
# --- Linking ---
target_link_libraries({config.Project.Name} PRIVATE {string.Join(" ", linkTargets)})
      ";
  }
}
