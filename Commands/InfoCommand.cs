using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Displays a summary of the project's configuration.
  /// </summary>
  /// <remarks>
  /// Shows key project information including name, type, number of dependencies,
  /// and number of scripts defined in package.toml.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project info
  /// </code>
  /// </example>
  [CliCommand(Name = "info", Description = "Display a summary of the project's configuration.", Parent = typeof(ProjectCommand))]
  public class InfoCommand
  {
    /// <summary>
    /// Displays project configuration summary.
    /// </summary>
    /// <returns>0 on success, 1 if project configuration cannot be loaded.</returns>
    public async Task<int> Run()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found or is missing project name.");
        return 1;
      }

      AnsiConsole.MarkupLine($"[bold]Project Name:[/] {config.Project.Name}");
      AnsiConsole.MarkupLine($"[bold]Project Type:[/] {config.Project.Type}");

      if (config.Dependencies.Count != 0)
      {
        AnsiConsole.MarkupLine("[bold]Dependencies:[/]" + " " + config.Dependencies.Count);
      }

      if (config.Scripts.Count != 0)
      {
        AnsiConsole.MarkupLine("[bold]Scripts:[/]" + " " + config.Scripts.Count);
      }

      return 0;
    }
  }
}
