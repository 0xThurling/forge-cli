using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Emits compiler/linker flags from the <c>build</c> section: named presets
/// expanded to per-compiler options, plus raw escape-hatch lists.
/// </summary>
/// <remarks>
/// Runs at priority 20 — after FetchContent/Conan (so fetched dependencies are
/// not affected) and before the project/target sections. <c>add_compile_options</c>
/// and <c>link_libraries</c> are directory-scoped, so everything created later
/// (the project target and the test target) inherits them.
/// </remarks>
public class FlagsSection : CMakeSectionBase
{
  public override string Name => "flags";

  public override int Priority => 20;

  public override bool IsEnabled(ProjectConfig config) => config.Build.HasAny;

  private sealed record Preset(
    List<string> Compile,
    List<string> Definitions,
    List<string> Link,
    bool Threads);

  // Flags are wrapped in a GNU/Clang guard so MSVC builds ignore them until
  // dedicated MSVC presets land.
  private static readonly Dictionary<string, Preset> Presets =
    new(StringComparer.OrdinalIgnoreCase)
    {
      ["production"] = new(["-O3"], ["NDEBUG"], [], false),
      ["debug"] = new(["-O0", "-g"], [], [], false),
      ["size"] = new(["-Os"], ["NDEBUG"], [], false),
      ["warnings"] = new(["-Wall", "-Wextra", "-Wpedantic"], [], [], false),
      ["warnings_as_errors"] = new(["-Werror"], [], [], false),
      ["concurrency"] = new([], [], [], true),
      ["simd"] = new(["-march=native"], [], [], false),
      ["lto"] = new(["-flto"], [], ["-flto"], false),
      ["asan"] = new(["-fsanitize=address", "-fno-omit-frame-pointer"], [],
                     ["-fsanitize=address"], false),
      ["ubsan"] = new(["-fsanitize=undefined", "-fno-omit-frame-pointer"], [],
                      ["-fsanitize=undefined"], false),
      ["tsan"] = new(["-fsanitize=thread"], [], ["-fsanitize=thread"], false),
      ["sanitize"] = new(["-fsanitize=address,undefined",
                          "-fno-omit-frame-pointer"], [],
                         ["-fsanitize=address,undefined"], false),
      ["coverage"] = new(["--coverage"], [], ["--coverage"], false),
      ["fast_math"] = new(["-ffast-math"], [], [], false),
      ["hardening"] = new(["-fstack-protector-strong"], ["_FORTIFY_SOURCE=2"],
                          [], false),
      ["no_exceptions"] = new(["-fno-exceptions", "-fno-rtti"], [], [], false),
    };

  private static string Guard(string flag) =>
    $"$<$<CXX_COMPILER_ID:GNU,Clang,AppleClang>:{flag}>";

  public override string Generate(ProjectConfig config)
  {
    var presetCompile = new List<string>();
    var defines = new List<string>();
    var presetLink = new List<string>();
    var libraries = new List<string>();
    var threads = false;

    foreach (var name in config.Build.Presets)
    {
      if (!Presets.TryGetValue(name, out var preset))
      {
        AnsiConsole.MarkupLine(
          $"[bold yellow]Warning:[/] Unknown build preset '{name}' (ignored).");
        continue;
      }

      presetCompile.AddRange(preset.Compile);
      defines.AddRange(preset.Definitions);
      presetLink.AddRange(preset.Link);
      threads |= preset.Threads;
    }

    // Raw escape hatches are emitted verbatim (no compiler guard).
    var rawCompile = config.Build.CompileOptions;
    defines.AddRange(config.Build.CompileDefinitions);
    var rawLink = config.Build.LinkOptions;
    libraries.AddRange(config.Build.LinkLibraries);

    var sb = new StringBuilder();
    sb.AppendLine("# --- Build Flags ---");

    if (threads)
    {
      sb.AppendLine("find_package(Threads REQUIRED)");
      sb.AppendLine("link_libraries(Threads::Threads)");
    }

    foreach (var d in defines.Distinct())
      sb.AppendLine($"add_compile_definitions({d})");

    foreach (var c in presetCompile.Distinct())
      sb.AppendLine($"add_compile_options(\"{Guard(c)}\")");

    foreach (var c in rawCompile.Distinct())
      sb.AppendLine($"add_compile_options(\"{c}\")");

    foreach (var l in presetLink.Distinct())
      sb.AppendLine($"add_link_options(\"{Guard(l)}\")");

    foreach (var l in rawLink.Distinct())
      sb.AppendLine($"add_link_options(\"{l}\")");

    foreach (var lib in libraries.Distinct())
      sb.AppendLine($"link_libraries({lib})");

    return sb.ToString();
  }
}
