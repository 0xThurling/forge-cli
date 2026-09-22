using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Runs clang-tidy over the project using the compile database Forge generates.
/// </summary>
/// <remarks>
/// Writes a `.clang-tidy` on first use when the project has none. Warnings are
/// treated as failures by default, so `forge lint` is usable as a CI gate;
/// pass <c>--allow-warnings</c> to report only.
/// </remarks>
[CliCommand(Name = "lint", Description = "Run clang-tidy over the project.", Parent = typeof(RootCommand))]
public class LintCommand
{
  [CliOption(Description = "Apply suggested fixes", Required = false)]
  public bool Fix { get; set; }

  [CliOption(Description = "Report warnings but exit 0", Required = false)]
  public bool AllowWarnings { get; set; }

  [CliOption(Description = "Paths to lint (default: src, test, bench)", Required = false)]
  public string[] Paths { get; set; } = [];

  [CliOption(Description = "Regex for the headers to report (default: this project only)", Required = false)]
  public string? HeaderFilter { get; set; }

  public async Task<int> RunAsync()
  {
    if (ProjectConfigManager.FindProjectRoot() is null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found.");
      return 1;
    }

    var tool = ToolLocator.Find("clang-tidy", "CLANG_TIDY");
    if (tool is null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] clang-tidy not found (set `CLANG_TIDY` or install it).");
      return 1;
    }

    // clang-tidy needs the compile database, which only exists after a build.
    var compileDb = Path.Combine("build", "compile_commands.json");
    if (!File.Exists(compileDb))
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] `build/compile_commands.json` not found — run `forge build` first.");
      return 1;
    }

    var files = SourceFiles.Gather(Paths);
    if (files.Count == 0)
    {
      AnsiConsole.MarkupLine("[yellow]No C++ sources found.[/]");
      return 0;
    }

    SourceFiles.EnsureClangTidy();

    var arguments = new List<string> { "-p", "build" };
    if (Fix)
      arguments.Add("--fix");
    if (!AllowWarnings)
      arguments.Add("--warnings-as-errors=*");

    // Scope header diagnostics to this project. Without it, clang-tidy walks
    // into every header the sources include — dependencies included — and a
    // consumer of a header library drowns in warnings it cannot fix.
    var headerFilter = HeaderFilter ?? DefaultHeaderFilter();
    if (headerFilter is not null)
      arguments.Add($"--header-filter={headerFilter}");

    arguments.AddRange(files);

    AnsiConsole.MarkupLine($"[dim]clang-tidy: {files.Count} file(s)[/]");
    var exit = await ToolLocator.RunAsync(tool, arguments);

    if (exit != 0)
    {
      AnsiConsole.MarkupLine("[bold red]clang-tidy reported problems.[/]");
      return exit;
    }

    AnsiConsole.MarkupLine("[green]No clang-tidy warnings.[/]");
    return 0;
  }

  /// <summary>
  /// Whether a `.clang-tidy` really sets <c>HeaderFilterRegex</c>. Comments are
  /// stripped first: the generated file mentions the key in a comment, and that
  /// must not look like the project taking over.
  /// </summary>
  private static bool SetsHeaderFilter(IEnumerable<string> lines)
  {
    foreach (var line in lines)
    {
      var code = line;
      var comment = code.IndexOf('#');
      if (comment >= 0)
        code = code[..comment];

      code = code.Trim();
      if (!code.StartsWith("HeaderFilterRegex", StringComparison.Ordinal))
        continue;

      var rest = code["HeaderFilterRegex".Length..].TrimStart();
      if (rest.StartsWith(':'))
        return true;
    }

    return false;
  }

  /// <summary>
  /// A pattern matching the project's own headers: the absolute root, so a
  /// dependency checked out elsewhere (or under build/_deps) is out of scope.
  /// </summary>
  /// <remarks>
  /// Null when the project's `.clang-tidy` sets <c>HeaderFilterRegex</c>: that
  /// is the project stating what it wants, and the command line would override
  /// it.
  /// </remarks>
  private static string? DefaultHeaderFilter()
  {
    const string configPath = ".clang-tidy";
    if (File.Exists(configPath))
    {
      try
      {
        if (SetsHeaderFilter(File.ReadAllLines(configPath)))
          return null;
      }
      catch (IOException)
      {
        // Unreadable config: fall through to the default.
      }
    }

    var root = Path.GetFullPath(Directory.GetCurrentDirectory())
      .TrimEnd(Path.DirectorySeparatorChar)
      .Replace('\\', '/');

    // Escape regex metacharacters (a project path may contain '+' or '.').
    var escaped = System.Text.RegularExpressions.Regex.Escape(root).Replace("\\", "/");
    return $"^{escaped}/(src|include|test|bench)/";
  }
}
