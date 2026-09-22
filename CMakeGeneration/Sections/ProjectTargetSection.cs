using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class ProjectTargetSection : CMakeSectionBase
{
  public override string Name => "project_target";

  public override int Priority => 30;

  /// <summary>The major component of a semantic version, or an empty string.</summary>
  private static string MajorOf(string version)
  {
    var dot = version.IndexOf('.');
    var major = dot < 0 ? version : version[..dot];
    return major.All(char.IsAsciiDigit) && major.Length > 0 ? major : string.Empty;
  }

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var sb = new StringBuilder();
    var linkage = config.Project.Linkage.ToUpper() ?? "STATIC";

    sb.AppendLine("# --- Project Target ---");
    sb.AppendLine($"file(GLOB_RECURSE SOURCES RELATIVE ${{PROJECT_SOURCE_DIR}} ${{PROJECT_SOURCE_DIR}}/src/*.cpp)");
    // A nested build directory (src/build, CMakeFiles, …) would otherwise
    // contribute its compiler probes as project sources.
    sb.AppendLine("list(FILTER SOURCES EXCLUDE REGEX \"/(build|build-[^/]*|CMakeFiles)/\")");

    // A precompiled header is attached to the target, so it is emitted here
    // rather than with the directory-scoped flags.
    var pchLines = config.Build.Pch.Length > 0
      ? $"  target_precompile_headers({config.Project.Name} PRIVATE ${{PROJECT_SOURCE_DIR}}/{config.Build.Pch})"
      : null;

    if (config.Project.Type == "executable")
    {
      sb.AppendLine("if(SOURCES)");
      sb.AppendLine($"  add_executable({config.Project.Name} ${{SOURCES}})");
      if (pchLines is not null)
        sb.AppendLine(pchLines);
      sb.AppendLine("else()");
      sb.AppendLine($"  message(WARNING \"No source files found in src/. Executable target '{config.Project.Name}' was not created.\")");
      sb.AppendLine("endif()");
    }
    else if (config.Project.Type == "library")
    {
      // Exported libraries need build-tree/install-tree interface paths
      // separated, otherwise CMake rejects the export and consumers inherit a
      // path into this checkout.
      var buildInterface = $"$<BUILD_INTERFACE:${{PROJECT_SOURCE_DIR}}/include>";
      var installInterface = "$<INSTALL_INTERFACE:include>";
      var version = config.Project.Version;
      var sversion = MajorOf(version);

      sb.AppendLine("if(SOURCES)");
      sb.AppendLine($"  add_library({config.Project.Name} {linkage} ${{SOURCES}})");
      if (pchLines is not null)
        sb.AppendLine(pchLines);
      if (config.Project.InstallHeaders)
      {
        sb.AppendLine($"  target_include_directories({config.Project.Name} PUBLIC {buildInterface} {installInterface})");
        sb.AppendLine($"  target_include_directories({config.Project.Name} PRIVATE ${{PROJECT_SOURCE_DIR}}/src)");
      }
      if (version.Length > 0)
      {
        sb.AppendLine($"  set_target_properties({config.Project.Name} PROPERTIES VERSION {version}" +
                      (sversion.Length > 0 ? $" SOVERSION {sversion}" : "") + ")");
      }
      sb.AppendLine($"  install(TARGETS {config.Project.Name} EXPORT {config.Project.Name}Config");
      sb.AppendLine("    ARCHIVE DESTINATION lib");
      sb.AppendLine("    LIBRARY DESTINATION lib");
      sb.AppendLine("    RUNTIME DESTINATION bin");
      sb.AppendLine("    INCLUDES DESTINATION include)");
      if (config.Project.InstallHeaders)
      {
        sb.AppendLine($"  install(DIRECTORY ${{PROJECT_SOURCE_DIR}}/include/ DESTINATION include)");
      }
      // A CMake package file, so `find_package(<name>)` works for consumers.
      sb.AppendLine($"  install(EXPORT {config.Project.Name}Config");
      sb.AppendLine($"    FILE {config.Project.Name}Config.cmake");
      sb.AppendLine($"    NAMESPACE {config.Project.Name}::");
      sb.AppendLine($"    DESTINATION lib/cmake/{config.Project.Name})");
      sb.AppendLine("else()");
      sb.AppendLine($"  message(WARNING \"No source files found in src/. Creating header-only INTERFACE library '{config.Project.Name}'.\")");
      sb.AppendLine($"  add_library({config.Project.Name} INTERFACE)");
      if (config.Project.InstallHeaders)
      {
        sb.AppendLine($"  target_include_directories({config.Project.Name} INTERFACE {buildInterface} {installInterface})");
        sb.AppendLine($"  install(DIRECTORY ${{PROJECT_SOURCE_DIR}}/include/ DESTINATION include)");
        sb.AppendLine($"  install(TARGETS {config.Project.Name} EXPORT {config.Project.Name}Config INCLUDES DESTINATION include)");
        sb.AppendLine($"  install(EXPORT {config.Project.Name}Config");
        sb.AppendLine($"    FILE {config.Project.Name}Config.cmake");
        sb.AppendLine($"    NAMESPACE {config.Project.Name}::");
        sb.AppendLine($"    DESTINATION lib/cmake/{config.Project.Name})");
      }
      sb.AppendLine("endif()");
    }

    return sb.ToString();
  }
}
