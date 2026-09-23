using System.Text;
using System.Text.RegularExpressions;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Emits the CMake contributed by Lua build scripts and the <c>custom</c>
/// section: variables, <c>find_package</c>, include/link directories,
/// definitions, compile options, libraries and subdirectories, plus any
/// pre-phase <c>forge.add_cmake(snippet, "pre")</c> snippets.
/// </summary>
/// <remarks>
/// Runs at priority 25 — after the flag presets (20) so script options can
/// override them, and <em>before</em> the project target (30) so variables,
/// packages and subdirectories exist by the time the target is defined and
/// linked. This is the supported hook for setup that git/local/Conan cannot
/// express (SDKs, toolchains, hand-written find modules).
/// </remarks>
public partial class LuaOptionsSection : CMakeSectionBase
{
  public override string Name => "lua-options";

  public override int Priority => 25;

  public override bool IsEnabled(BuildContext context) =>
    !context.LuaOptions.IsEmpty ||
    context.Config.Custom.Count != 0 ||
    context.CustomCmakeSnippetsPre.Count != 0;

  // `custom` keys become CMake variables, so only identifiers are emitted.
  [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
  private static partial Regex VariableNamePattern();

  /// <summary>
  /// Wraps a value as a CMake quoted argument. Backslashes and quotes must be
  /// escaped: a raw value such as <c>say "hi"</c> would otherwise emit a
  /// malformed <c>set()</c> line.
  /// </summary>
  private static string Quote(string value) =>
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var options = context.LuaOptions;
    var sb = new StringBuilder();

    sb.AppendLine("# --- Lua build-script options ---");

    // forge.lua `custom = { ... }` first, then script variables, so a script
    // can override a declarative default.
    foreach (var (key, value) in config.Custom)
    {
      if (VariableNamePattern().IsMatch(key))
        sb.AppendLine($"set({key} {Quote(value)})");
    }

    foreach (var (key, value) in options.Variables)
      sb.AppendLine($"set({key} {Quote(value)})");

    // Cache variables carry a type and force their value, so they are visible to
    // toolchain files and to a dependency's option() calls.
    foreach (var (key, value) in options.CacheVariables)
      sb.AppendLine($"set({key} {Quote(value)} CACHE STRING \"\" FORCE)");

    foreach (var package in options.FindPackages)
      sb.AppendLine($"find_package({package} REQUIRED)");

    foreach (var dir in options.Subdirectories)
      sb.AppendLine($"add_subdirectory({dir})");

    foreach (var dir in options.IncludeDirectories)
      sb.AppendLine($"include_directories({dir})");

    foreach (var dir in options.LinkDirectories)
      sb.AppendLine($"link_directories({dir})");

    foreach (var definition in options.Definitions)
      sb.AppendLine($"add_compile_definitions({definition})");

    foreach (var flag in options.CompileOptions)
      sb.AppendLine($"add_compile_options(\"{flag}\")");

    foreach (var library in options.LinkLibraries)
      sb.AppendLine($"link_libraries({library})");

    foreach (var snippet in context.CustomCmakeSnippetsPre)
      sb.AppendLine(snippet.Replace("${PROJECT_NAME}", config.Project.Name));

    return sb.ToString();
  }
}
