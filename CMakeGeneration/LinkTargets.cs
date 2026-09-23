using forge.Models;

namespace forge.CMakeGeneration;

/// <summary>
/// The CMake targets a project's own targets link: its direct dependencies,
/// what Conan reported, the imported pkg-config targets and the vcpkg packages.
/// </summary>
/// <remarks>
/// Shared by the link line of the main target and by every extra target, so a
/// project with several targets links the same things everywhere.
/// </remarks>
internal static class LinkTargets
{
  public static List<string> For(ProjectConfig config, BuildContext context)
  {
    var targets = new List<string>();

    foreach (var dep in config.Dependencies)
    {
      // Only what the generated CMake actually fetches can be linked: a
      // dependency without a source would become `-l<name>` and fail at link
      // time instead of being reported.
      var hasSource = !string.IsNullOrWhiteSpace(dep.Value.Path) ||
                      (!string.IsNullOrWhiteSpace(dep.Value.Git) &&
                       !string.IsNullOrWhiteSpace(dep.Value.Tag));
      if (dep.Key == "googletest" || !hasSource)
        continue;

      targets.Add(string.IsNullOrEmpty(dep.Value.Target) ? dep.Key : dep.Value.Target);
    }

    // From Conan
    targets.AddRange(context.LinkDependencies);

    // From pkg-config: the imported target pkg_check_modules creates.
    foreach (var module in config.PkgConfigDependencies)
      targets.Add(Sections.PkgConfigSection.TargetFor(module));

    // From vcpkg: the declared target (vcpkg cannot be queried for them
    // without running it, so they are explicit).
    foreach (var (name, dependency) in config.VcpkgDependencies)
      targets.Add(dependency.Target.Length > 0 ? dependency.Target : name);

    return targets;
  }
}
