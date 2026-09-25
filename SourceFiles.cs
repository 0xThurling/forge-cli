using System.Diagnostics;
using Spectre.Console;

namespace forge;

/// <summary>
/// Locates and runs external tools (clang-format, clang-tidy, …).
/// </summary>
internal static class ToolLocator
{
  /// <summary>
  /// Finds a tool on <c>PATH</c> (honouring an environment override), or null.
  /// </summary>
  public static string? Find(string executable, string? environmentVariable = null)
  {
    if (!string.IsNullOrWhiteSpace(environmentVariable))
    {
      var overridden = Environment.GetEnvironmentVariable(environmentVariable);
      if (!string.IsNullOrWhiteSpace(overridden) && File.Exists(overridden))
        return overridden;
    }

    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrEmpty(path))
      return null;

    foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
      try
      {
        var candidate = Path.Combine(dir, executable);
        if (File.Exists(candidate))
          return candidate;
        if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe"))
          return candidate + ".exe";
      }
      catch (ArgumentException)
      {
        // Malformed PATH entry; ignore it.
      }
    }
    return null;
  }

  /// <summary>Runs a tool with the given arguments, streaming its output.</summary>
  public static async Task<int> RunAsync(string executable, IReadOnlyList<string> arguments)
  {
    var psi = new ProcessStartInfo(executable)
    {
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in arguments)
      psi.ArgumentList.Add(argument);

    using var process = Process.Start(psi);
    if (process == null)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] could not start `{executable}`.");
      return 1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
  }
}

/// <summary>
/// The project's C++ sources, and the tool configuration files Forge writes on
/// first use.
/// </summary>
internal static class SourceFiles
{
  private static readonly string[] Extensions =
    [".h", ".hpp", ".hxx", ".hh", ".c", ".cc", ".cpp", ".cxx"];

  private static readonly string[] DefaultDirectories = ["src", "test", "bench"];

  /// <summary>
  /// Every C++ source under the given paths (defaults to src/test/bench),
  /// skipping build directories.
  /// </summary>
  public static List<string> Gather(string[] paths)
  {
    var roots = paths.Length > 0 ? paths : DefaultDirectories;
    var files = new List<string>();

    foreach (var root in roots)
    {
      if (File.Exists(root))
      {
        files.Add(root);
        continue;
      }
      if (!Directory.Exists(root))
        continue;

      files.AddRange(Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(file => Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
        .Where(file => !file.Split(Path.DirectorySeparatorChar)
          .Any(part => part is "build" or "CMakeFiles" || part.StartsWith("build-"))));
    }

    files.Sort(StringComparer.Ordinal);
    return files;
  }

  /// <summary>
  /// True when <paramref name="root"/>'s <c>src/</c> holds a <c>.cpp</c> file,
  /// skipping build directories — the condition the generated CMake's
  /// <c>SOURCES</c> glob evaluates. A library with none is created as an
  /// INTERFACE target and produces no artifact.
  /// </summary>
  public static bool HasSources(string root = ".")
  {
    var source = Path.Combine(root, "src");
    if (!Directory.Exists(source))
      return false;

    return Directory
      .EnumerateFiles(source, "*", SearchOption.AllDirectories)
      .Where(file => !file.Split(Path.DirectorySeparatorChar)
        .Any(part => part is "build" or "CMakeFiles" || part.StartsWith("build-")))
      .Any(file => Path.GetExtension(file).Equals(".cpp", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// The entries a project's `.gitignore` needs for files Forge and the build
  /// generate. Committing them only produces churn (and the paths differ per
  /// machine for the compile database).
  /// </summary>
  public static readonly string[] GeneratedIgnoreEntries =
    ["build/", "lib/", "compile_commands.json", "CMakePresets.json", "conanfile.txt", ".config/forge/hot/"];

  /// <summary>The `.gitignore` written by `forge create`.</summary>
  public static string DefaultGitIgnore() => string.Join("\n", GeneratedIgnoreEntries) + "\n";

  /// <summary>Writes a conservative `.clang-format` when the project has none.</summary>
  public static void EnsureClangFormat()
  {
    const string path = ".clang-format";
    if (File.Exists(path))
      return;

    File.WriteAllText(path, """
      # Written by `forge format`; edit freely.
      BasedOnStyle: LLVM
      IndentWidth: 2
      ColumnLimit: 100
      SortIncludes: CaseSensitive
      """);
    AnsiConsole.MarkupLine("[dim]Wrote .clang-format (edit it to taste).[/]");
  }

  /// <summary>Writes a conservative `.clang-tidy` when the project has none.</summary>
  public static void EnsureClangTidy()
  {
    const string path = ".clang-tidy";
    if (File.Exists(path))
      return;

    File.WriteAllText(path, """
      # Written by `forge lint`; edit freely.
      Checks: >
        bugprone-*,
        performance-*,
        clang-analyzer-*,
        -bugprone-easily-swappable-parameters
      WarningsAsErrors: ''
      # Header filtering is left to `forge lint`, which scopes diagnostics to
      # this project. Set HeaderFilterRegex here to take over.
      """);
    AnsiConsole.MarkupLine("[dim]Wrote .clang-tidy (edit it to taste).[/]");
  }
}
