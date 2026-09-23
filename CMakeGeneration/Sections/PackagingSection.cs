using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Emits the CPack configuration, so a versioned project can be packaged with
/// <c>forge publish</c> (or plain <c>cpack</c>).
/// </summary>
/// <remarks>
/// Runs at priority 55 — after the project target (30) and the install rules
/// that come with it, because <c>install(TARGETS …)</c> has to follow the
/// target it installs. Only versioned projects are packaged: without a version
/// the archive name would be meaningless.
/// </remarks>
public class PackagingSection : CMakeSectionBase
{
  public override string Name => "packaging";

  public override int Priority => 55;

  public override bool IsEnabled(BuildContext context) =>
    !string.IsNullOrWhiteSpace(context.Config.Project.Version);

  /// <summary>Escapes a value for a CMake quoted argument.</summary>
  private static string Escape(string value) =>
    value.Replace("\\", "\\\\").Replace("\"", "\\\"");

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var body = new StringBuilder();
    var name = config.Project.Name;
    var version = config.Project.Version;

    body.AppendLine($"set(CPACK_PACKAGE_NAME \"{name}\")");
    body.AppendLine($"set(CPACK_PACKAGE_VERSION \"{version}\")");

    if (!string.IsNullOrWhiteSpace(config.Project.Description))
      body.AppendLine($"set(CPACK_PACKAGE_DESCRIPTION_SUMMARY \"{Escape(config.Project.Description)}\")");
    if (!string.IsNullOrWhiteSpace(config.Project.Contact))
      body.AppendLine($"set(CPACK_PACKAGE_CONTACT \"{Escape(config.Project.Contact)}\")");

    body.AppendLine("set(CPACK_GENERATOR \"TGZ\")");

    // Runtime dependencies: without these, a package installs but declares
    // nothing it needs (CPack warns about exactly that).
    var debDepends = config.Project.PackageDepends.Concat(config.Project.DebDepends).ToList();
    var rpmDepends = config.Project.PackageDepends.Concat(config.Project.RpmDepends).ToList();
    if (debDepends.Count > 0)
      body.AppendLine($"set(CPACK_DEBIAN_PACKAGE_DEPENDS \"{string.Join(", ", debDepends)}\")");
    if (rpmDepends.Count > 0)
      body.AppendLine($"set(CPACK_RPM_PACKAGE_REQUIRES \"{string.Join(", ", rpmDepends)}\")");

    // Libraries install themselves (with their export set); an executable needs
    // a rule of its own or the package would be empty.
    if (config.Project.Type == "executable")
      body.AppendLine($"install(TARGETS {name} RUNTIME DESTINATION bin)");

    body.AppendLine("include(CPack)");

    // Packaging belongs to the project itself: a consumer that adds it as a
    // dependency must not inherit these settings or its `package` target.
    var sb = new StringBuilder();
    sb.AppendLine("# --- Packaging (CPack) ---");
    sb.AppendLine("# Only the top-level project packages itself; skipped when this project");
    sb.AppendLine("# is used as a dependency.");
    sb.AppendLine("if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)");
    foreach (var line in body.ToString().Split('\n'))
      sb.AppendLine(line.Length == 0 ? string.Empty : "  " + line);
    sb.AppendLine("endif()");
    return sb.ToString();
  }
}
