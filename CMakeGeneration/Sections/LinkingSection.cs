using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class LinkingSection : CMakeSectionBase
{
  public override string Name => "linking";

  public override int Priority => 40;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
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
    linkTargets.AddRange(context.LinkDependencies);

    // From pkg-config: the imported target pkg_check_modules creates.
    foreach (var module in config.PkgConfigDependencies)
      linkTargets.Add(PkgConfigSection.TargetFor(module));

    // From vcpkg: the declared target (vcpkg cannot be queried for them
    // without running it, so they are explicit).
    foreach (var (name, dependency) in config.VcpkgDependencies)
      linkTargets.Add(dependency.Target.Length > 0 ? dependency.Target : name);

    if (linkTargets.Count == 0) return string.Empty;

    return $@"
# --- Linking ---
target_link_libraries({config.Project.Name} PRIVATE {string.Join(" ", linkTargets)})
      ";
  }
}
