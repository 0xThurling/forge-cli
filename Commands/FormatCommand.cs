using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Formats the project's C++ sources with clang-format.
/// </summary>
/// <remarks>
/// Writes a `.clang-format` on first use when the project has none, then
/// formats every source under <c>src</c>, <c>test</c> and <c>bench</c>.
/// <c>--check</c> reports instead of rewriting, which is what CI wants.
/// </remarks>
[CliCommand(Name = "format", Description = "Format C++ sources with clang-format.", Parent = typeof(RootCommand))]
public class FormatCommand
{
  [CliOption(Description = "Check only; fail when something is unformatted", Required = false)]
  public bool Check { get; set; }

  [CliOption(Description = "Paths to format (default: src, test, bench)", Required = false)]
  public string[] Paths { get; set; } = [];

  public async Task<int> RunAsync()
  {
    if (ProjectConfigManager.FindProjectRoot() is null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found.");
      return 1;
    }

    var tool = ToolLocator.Find("clang-format", "CLANG_FORMAT");
    if (tool is null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] clang-format not found (set `CLANG_FORMAT` or install it).");
      return 1;
    }

    var files = SourceFiles.Gather(Paths);
    if (files.Count == 0)
    {
      AnsiConsole.MarkupLine("[yellow]No C++ sources found.[/]");
      return 0;
    }

    SourceFiles.EnsureClangFormat();

    var arguments = new List<string>();
    if (Check)
      arguments.AddRange(["--dry-run", "--Werror"]);
    else
      arguments.Add("-i");
    arguments.AddRange(files);

    var exit = await ToolLocator.RunAsync(tool, arguments);
    if (exit != 0)
    {
      AnsiConsole.MarkupLine(Check
        ? $"[bold red]{files.Count} file(s) need formatting[/] (run `forge format`)."
        : "[bold red]clang-format failed.[/]");
      return exit == 0 ? 1 : exit;
    }

    AnsiConsole.MarkupLine(Check
      ? $"[green]All {files.Count} file(s) are formatted.[/]"
      : $"[green]Formatted {files.Count} file(s).[/]");
    return 0;
  }
}
