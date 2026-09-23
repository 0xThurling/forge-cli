using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Turns the current directory into a Forge project, keeping what is already
/// there.
/// </summary>
/// <remarks>
/// Where <c>forge create</c> makes a new directory, <c>forge init</c> adopts an
/// existing one: an empty folder, or a checkout that already has sources. It
/// writes the layout, a <c>forge.lua</c> and — only when there is none — a
/// <c>.gitignore</c>; existing sources are never touched.
/// </remarks>
/// <example>
/// <code>
/// cd existing-checkout
/// forge init --name mylib --type library
/// forge build
/// </code>
/// </example>
[CliCommand(Name = "init", Description = "Set up a Forge project in the current directory.", Parent = typeof(RootCommand))]
public class InitCommand
{
  [CliOption(Description = "Project name (default: the directory name)", Required = false)]
  public string? Name { get; set; }

  [CliOption(Description = "Type of project to create (executable or library).", Required = false)]
  public string Type { get; set; } = "executable";

  [CliOption(Description = "C++ standard (default: 20)", Required = false)]
  public string Standard { get; set; } = "20";

  [CliOption(Description = "Enable the test target", Required = false)]
  public bool Testing { get; set; }

  public Task<int> RunAsync()
  {
    var directory = Directory.GetCurrentDirectory();
    var name = string.IsNullOrWhiteSpace(Name)
      ? new DirectoryInfo(directory).Name
      : Name!;

    if (File.Exists("forge.lua"))
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] this directory already has a `forge.lua` — " +
        "`forge init` is for directories that are not Forge projects yet.");
      return Task.FromResult(1);
    }

    var type = Type.Trim().ToLowerInvariant();
    if (type is not ("executable" or "library"))
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] unknown type `{Type}` — use `executable` or `library`.");
      return Task.FromResult(1);
    }

    if (name.Any(char.IsWhiteSpace))
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] `{name}` cannot be a project name (spaces) — pass `--name <name>`.");
      return Task.FromResult(1);
    }

    try
    {
      ProjectScaffolder.Scaffold(".", name, type, Standard, Testing);
    }
    catch (Exception exception)
    {
      AnsiConsole.MarkupLine($"[red]Error: {exception.Message}[/]");
      return Task.FromResult(1);
    }

    AnsiConsole.MarkupLine($"[bold green]Initialised `[bold yellow]{name}[/]` in {directory}.[/]");
    AnsiConsole.MarkupLine("[dim]Next: `forge build` (and `forge doctor` if the directory was not empty).[/]");
    return Task.FromResult(0);
  }
}
