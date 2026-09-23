using System.Text;
using forge.Models;
using Spectre.Console;

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
      AppendModuleFileSet(sb, config, config.Project.Name, ["src"]);
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
      AppendModuleFileSet(sb, config, config.Project.Name, ["src"]);
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

    AppendExtraTargets(sb, context);
    return sb.ToString();
  }

  /// <summary>
  /// Emits the <c>CXX_MODULES</c> file set for a target: module interface units
  /// (<c>.cppm</c>, <c>.ixx</c>) must be listed there rather than as ordinary
  /// sources — CMake rejects them otherwise ("provides the module but it is not
  /// found in a FILE_SET of type CXX_MODULES"). Only emitted when the project
  /// asks for modules, so nothing changes for a project that has none.
  /// </summary>
  public static void AppendModuleFileSet(
    StringBuilder sb, ProjectConfig config, string target, IEnumerable<string> directories)
  {
    if (!config.Build.Modules)
      return;

    var roots = directories
      .Select(directory => "${CMAKE_CURRENT_SOURCE_DIR}/" + directory.Replace('\\', '/').TrimEnd('/'))
      .ToList();

    var variable = new string(target
      .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
      .ToArray()) + "_MODULES";

    sb.AppendLine($"  file(GLOB_RECURSE {variable} " +
                  string.Join(" ", roots.Select(root => $"\"{root}/*.cppm\" \"{root}/*.ixx\"")) + ")");
    sb.AppendLine($"  if({variable})");
    sb.AppendLine($"    target_sources({target} PRIVATE FILE_SET CXX_MODULES");
    sb.AppendLine($"      BASE_DIRS {string.Join(" ", roots.Select(root => $"\"{root}\"") )}");
    sb.AppendLine($"      FILES ${{{variable}}})");
    sb.AppendLine("  endif()");
  }

  /// <summary>
  /// Emits one block per extra target (<c>targets</c> in <c>forge.lua</c>).
  /// Each has its own source glob, the same dependencies the main target links,
  /// and — for libraries — install rules. The project's own target above is
  /// untouched by this.
  /// </summary>
  private static void AppendExtraTargets(StringBuilder sb, BuildContext context)
  {
    var config = context.Config;
    var links = LinkTargets.For(config, context);
    var pch = config.Build.Pch.Length > 0 ? config.Build.Pch : null;

    // The project's own libraries are part of it: every target links them
    // (a library links its siblings, not itself).
    var libraries = config.Targets
      .Where(target => target.Type == "library")
      .Select(target => target.Name)
      .ToList();

    foreach (var target in config.Targets)
    {
      var targetLinks = new List<string>(links);
      targetLinks.AddRange(libraries.Where(library =>
        !string.Equals(library, target.Name, StringComparison.OrdinalIgnoreCase)));
      var variable = new string(target.Name
        .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
        .ToArray()) + "_SOURCES";

      sb.AppendLine();
      sb.AppendLine($"# --- Target: {target.Name} ({target.Type}) ---");

      // Each entry is a directory to glob or a single file.
      var patterns = new List<string>();
      string? includeDirectory = null;
      foreach (var source in target.Sources)
      {
        var normalized = source.Replace('\\', '/').TrimEnd('/');
        if (File.Exists(source))
        {
          patterns.Add($"${{PROJECT_SOURCE_DIR}}/{normalized}");
          continue;
        }

        if (!Directory.Exists(source))
        {
          AnsiConsole.MarkupLine(
            $"[bold yellow]Warning:[/] target '{target.Name}' lists '{source}', which does not exist.");
        }

        patterns.Add($"${{PROJECT_SOURCE_DIR}}/{normalized}/*.cpp");
        includeDirectory ??= normalized;
      }

      sb.AppendLine(
        $"file(GLOB_RECURSE {variable} RELATIVE ${{PROJECT_SOURCE_DIR}} {string.Join(" ", patterns)})");
      sb.AppendLine($"list(FILTER {variable} EXCLUDE REGEX \"/(build|build-[^/]*|CMakeFiles)/\")");
      sb.AppendLine($"if({variable})");

      if (target.Type == "executable")
      {
        sb.AppendLine($"  add_executable({target.Name} ${{{variable}}})");
        if (pch is not null)
          sb.AppendLine($"  target_precompile_headers({target.Name} PRIVATE ${{PROJECT_SOURCE_DIR}}/{pch})");
        AppendModuleFileSet(sb, config, target.Name, target.Sources.Where(Directory.Exists));
        if (targetLinks.Count > 0)
          sb.AppendLine($"  target_link_libraries({target.Name} PRIVATE {string.Join(" ", targetLinks)})");
      }
      else
      {
        var linkage = target.Linkage.Equals("shared", StringComparison.OrdinalIgnoreCase) ? "SHARED" : "STATIC";
        sb.AppendLine($"  add_library({target.Name} {linkage} ${{{variable}}})");
        if (pch is not null)
          sb.AppendLine($"  target_precompile_headers({target.Name} PRIVATE ${{PROJECT_SOURCE_DIR}}/{pch})");
        if (includeDirectory is not null)
          sb.AppendLine($"  target_include_directories({target.Name} PUBLIC ${{PROJECT_SOURCE_DIR}}/{includeDirectory})");
        if (targetLinks.Count > 0)
          sb.AppendLine($"  target_link_libraries({target.Name} PUBLIC {string.Join(" ", targetLinks)})");

        if (target.Installs)
        {
          sb.AppendLine($"  install(TARGETS {target.Name}");
          sb.AppendLine("    ARCHIVE DESTINATION lib");
          sb.AppendLine("    LIBRARY DESTINATION lib");
          sb.AppendLine("    RUNTIME DESTINATION bin)");
        }
      }

      sb.AppendLine("else()");
      sb.AppendLine($"  message(WARNING \"No source files found for target '{target.Name}'. It was not created.\")");
      sb.AppendLine("endif()");
    }
  }
}
