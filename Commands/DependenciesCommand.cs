using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Lists all project dependencies from forge.lua.
  /// </summary>
  /// <remarks>
  /// Displays a table showing all Git-based dependencies including their names,
  /// repository URLs, and version tags.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project dependencies
  /// </code>
  /// </example>
  [CliCommand(Name = "dependencies", Description = "List the project's dependencies and their versions.", Parent = typeof(ProjectCommand))]
  public class DependenciesCommand
  {
    /// <summary>
    /// Lists all dependencies in a formatted table.
    /// </summary>
    /// <returns>0 on success, 1 if project configuration cannot be loaded.</returns>
    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      if (config.Dependencies.Count == 0)
      {
        AnsiConsole.MarkupLine("[yellow]No dependencies defined in forge.lua.[/]");
        return 0;
      }

      var table = new Table();
      table.AddColumn("Name");
      table.AddColumn("Git");
      table.AddColumn("Tag");

      foreach (var dependency in config.Dependencies)
      {
        table.AddRow(dependency.Key, dependency.Value.Git, dependency.Value.Tag);
      }

      AnsiConsole.Write(table);

      return 0;
    }
  }
}
