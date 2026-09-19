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
    bool Threads,
    List<string> MsvcCompile,
    List<string> MsvcLink);

  // GNU/Clang flags are wrapped in a compiler guard; MSVC gets its own
  // equivalents where one exists (none for the sanitizers that MSVC lacks).
  private static readonly Dictionary<string, Preset> Presets =
    new(StringComparer.OrdinalIgnoreCase)
    {
      ["production"] = new(["-O3"], ["NDEBUG"], [], false, ["/O2"], []),
      ["debug"] = new(["-O0", "-g"], [], [], false, ["/Od", "/Zi"], ["/DEBUG"]),
      ["size"] = new(["-Os"], ["NDEBUG"], [], false, ["/O1"], []),
      ["warnings"] = new(["-Wall", "-Wextra", "-Wpedantic"], [], [], false,
                         ["/W4"], []),
      ["warnings_as_errors"] = new(["-Werror"], [], [], false, ["/WX"], []),
      ["concurrency"] = new([], [], [], true, [], []),
      ["simd"] = new(["-march=native"], [], [], false, ["/arch:AVX2"], []),
      ["lto"] = new(["-flto"], [], ["-flto"], false, ["/GL"], ["/LTCG"]),
      ["asan"] = new(["-fsanitize=address", "-fno-omit-frame-pointer"], [],
                     ["-fsanitize=address"], false,
                     ["/fsanitize=address"], ["/fsanitize=address"]),
      ["ubsan"] = new(["-fsanitize=undefined", "-fno-omit-frame-pointer"], [],
                      ["-fsanitize=undefined"], false, [], []),
      ["tsan"] = new(["-fsanitize=thread"], [], ["-fsanitize=thread"], false,
                     [], []),
      ["sanitize"] = new(["-fsanitize=address,undefined",
                          "-fno-omit-frame-pointer"], [],
                         ["-fsanitize=address,undefined"], false, [], []),
      ["coverage"] = new(["--coverage"], [], ["--coverage"], false, [], []),
      ["fast_math"] = new(["-ffast-math"], [], [], false, ["/fp:fast"], []),
      ["hardening"] = new(["-fstack-protector-strong"], ["_FORTIFY_SOURCE=2"],
                          [], false, ["/GS"], []),
      ["no_exceptions"] = new(["-fno-exceptions", "-fno-rtti"], [], [], false,
                              ["/EHs-c-", "/GR-"], []),
    };

  private static string Guard(string flag) =>
    $"$<$<CXX_COMPILER_ID:GNU,Clang,AppleClang>:{flag}>";

  private static string MsvcGuard(string flag) =>
    $"$<$<CXX_COMPILER_ID:MSVC>:{flag}>";

  public override string Generate(ProjectConfig config)
  {
    var presetCompile = new List<string>();
    var defines = new List<string>();
    var presetLink = new List<string>();
    var msvcCompile = new List<string>();
    var msvcLink = new List<string>();
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
      msvcCompile.AddRange(preset.MsvcCompile);
      msvcLink.AddRange(preset.MsvcLink);
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

    foreach (var c in msvcCompile.Distinct())
      sb.AppendLine($"add_compile_options(\"{MsvcGuard(c)}\")");

    foreach (var c in rawCompile.Distinct())
      sb.AppendLine($"add_compile_options(\"{c}\")");

    foreach (var l in presetLink.Distinct())
      sb.AppendLine($"add_link_options(\"{Guard(l)}\")");

    foreach (var l in msvcLink.Distinct())
      sb.AppendLine($"add_link_options(\"{MsvcGuard(l)}\")");

    foreach (var l in rawLink.Distinct())
      sb.AppendLine($"add_link_options(\"{l}\")");

    foreach (var lib in libraries.Distinct())
      sb.AppendLine($"link_libraries({lib})");

    return sb.ToString();
  }
}
