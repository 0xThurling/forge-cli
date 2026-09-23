using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge;

/// <summary>
/// vcpkg integration (manifest mode): writes <c>vcpkg.json</c> and finds the
/// toolchain file CMake needs to resolve the packages.
/// </summary>
/// <remarks>
/// Manifest mode means Forge never runs vcpkg itself: passing the toolchain
/// file makes CMake install the manifest's dependencies during configure. The
/// declared <c>target</c> is what gets linked, since vcpkg cannot be queried
/// for targets without running it.
/// </remarks>
public static class VcpkgManager
{
  public const string ManifestFileName = "vcpkg.json";

  /// <summary>
  /// The vcpkg checkout to use: <c>vcpkg_root</c>, then <c>$VCPKG_ROOT</c>,
  /// then <c>external/vcpkg</c>.
  /// </summary>
  public static string? Root(ProjectConfig config)
  {
    if (!string.IsNullOrWhiteSpace(config.VcpkgRoot))
      return Directory.Exists(config.VcpkgRoot) ? config.VcpkgRoot : null;

    var fromEnv = Environment.GetEnvironmentVariable("VCPKG_ROOT");
    if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
      return fromEnv;

    var local = Path.Combine("external", "vcpkg");
    return Directory.Exists(local) ? local : null;
  }

  /// <summary>The toolchain file to hand to CMake, or null when unavailable.</summary>
  public static string? ToolchainFile(ProjectConfig config)
  {
    var root = Root(config);
    if (root is null)
      return null;

    var file = Path.Combine(root, "scripts", "buildsystems", "vcpkg.cmake");
    return File.Exists(file) ? file : null;
  }

  /// <summary>
  /// Writes <c>vcpkg.json</c> for the declared dependencies.
  /// </summary>
  /// <remarks>
  /// vcpkg package names must be lowercase and may only contain digits, dashes
  /// and letters, so the project name is sanitized rather than copied blindly.
  /// </remarks>
  public static void WriteManifest(ProjectConfig config)
  {
    var name = SanitizePackageName(config.Project.Name);
    var sb = new StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine($"  \"name\": {JsonOutput.Quote(name)},");
    sb.AppendLine($"  \"version-string\": {JsonOutput.Quote(string.IsNullOrWhiteSpace(config.Project.Version) ? "0.0.0" : config.Project.Version)},");

    if (!string.IsNullOrWhiteSpace(config.VcpkgBaseline))
      sb.AppendLine($"  \"builtin-baseline\": {JsonOutput.Quote(config.VcpkgBaseline)},");

    sb.AppendLine("  \"dependencies\": [");
    var names = config.VcpkgDependencies.Keys.ToList();
    for (var i = 0; i < names.Count; i++)
    {
      var dependency = config.VcpkgDependencies[names[i]];
      var comma = i == names.Count - 1 ? string.Empty : ",";
      if (string.IsNullOrWhiteSpace(dependency.Version))
        sb.AppendLine($"    {JsonOutput.Quote(SanitizePackageName(names[i]))}{comma}");
      else
        sb.AppendLine($"    {{ \"name\": {JsonOutput.Quote(SanitizePackageName(names[i]))}, \"version>=\": {JsonOutput.Quote(dependency.Version)} }}{comma}");
    }
    sb.AppendLine("  ]");
    sb.AppendLine("}");

    File.WriteAllText(ManifestFileName, sb.ToString());
  }

  private static string SanitizePackageName(string name)
  {
    var sb = new StringBuilder(name.Length);
    foreach (var c in name.ToLowerInvariant())
      sb.Append(char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-');
    return sb.ToString();
  }

  /// <summary>
  /// Reports whether the declared vcpkg setup is usable, printing the reason
  /// when it is not.
  /// </summary>
  public static bool Validate(ProjectConfig config)
  {
    if (config.VcpkgDependencies.Count == 0)
      return true;

    if (ToolchainFile(config) is not null)
      return true;

    var root = Root(config);
    if (root is null)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] vcpkg dependencies are declared but no vcpkg checkout was found. " +
        "Set `vcpkg_root` in forge.lua, export `VCPKG_ROOT`, or clone it into `external/vcpkg`.");
    }
    else
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] `{Path.Combine(root, "scripts", "buildsystems", "vcpkg.cmake")}` not found — " +
        "`vcpkg_root` does not look like a vcpkg checkout.");
    }
    return false;
  }
}
