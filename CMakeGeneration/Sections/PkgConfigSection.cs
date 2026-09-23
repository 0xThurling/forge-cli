using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Resolves pkg-config modules declared in <c>dependencies.pkgconfig</c>.
/// </summary>
/// <remarks>
/// Runs at priority 7 — with the other package-manager sections, before the
/// project target — so the imported targets exist for the link line. Each
/// module becomes <c>PkgConfig::&lt;VAR&gt;</c>, where the variable name is the
/// module name upper-cased with non-alphanumerics replaced by underscores
/// (pkg-config's own convention).
/// </remarks>
public class PkgConfigSection : CMakeSectionBase
{
  public override string Name => "pkgconfig";

  public override int Priority => 7;

  public override bool IsEnabled(BuildContext context) =>
    context.Config.PkgConfigDependencies.Count != 0;

  /// <summary>The imported target name for a pkg-config module.</summary>
  public static string TargetFor(string module) =>
    "PkgConfig::" + VariableFor(module);

  /// <summary>The pkg-config variable name for a module.</summary>
  public static string VariableFor(string module)
  {
    var sb = new StringBuilder(module.Length);
    foreach (var c in module.ToUpperInvariant())
      sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
    return sb.ToString();
  }

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var sb = new StringBuilder();
    sb.AppendLine("# --- pkg-config dependencies ---");
    sb.AppendLine("find_package(PkgConfig REQUIRED)");

    foreach (var module in config.PkgConfigDependencies)
      sb.AppendLine($"pkg_check_modules({VariableFor(module)} REQUIRED IMPORTED_TARGET {module})");

    return sb.ToString();
  }
}
