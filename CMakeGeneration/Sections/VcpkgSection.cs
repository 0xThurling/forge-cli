using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Emits <c>find_package</c> for the declared vcpkg dependencies.
/// </summary>
/// <remarks>
/// Runs at priority 6 — with the other package-manager sections, before the
/// project target — so the imported targets exist for the link line. The
/// packages themselves are installed by vcpkg's toolchain during configure.
/// </remarks>
public class VcpkgSection : CMakeSectionBase
{
  public override string Name => "vcpkg";

  public override int Priority => 6;

  public override bool IsEnabled(BuildContext context) =>
    context.Config.VcpkgDependencies.Count != 0;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var sb = new StringBuilder();
    sb.AppendLine("# --- vcpkg dependencies ---");

    foreach (var (name, dependency) in config.VcpkgDependencies)
    {
      var package = dependency.Target.Length > 0
        ? dependency.Target.Split("::")[0]
        : name;
      sb.AppendLine($"find_package({package} REQUIRED)");
    }

    return sb.ToString();
  }
}
