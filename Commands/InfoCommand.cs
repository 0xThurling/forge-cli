using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Displays a summary of the project's configuration.
  /// </summary>
  /// <remarks>
  /// Shows key project information including name, type, number of dependencies,
  /// and number of scripts defined in forge.lua.
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
    /// <summary>Print machine-readable JSON instead of a report.</summary>
    [CliOption(Description = "Print JSON", Required = false)]
    public bool Json { get; set; }

    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      if (Json)
      {
        var json = new System.Text.StringBuilder();
        json.Append('{');
        json.Append($"\"name\":{JsonOutput.Quote(config.Project.Name)},");
        json.Append($"\"type\":{JsonOutput.Quote(config.Project.Type)},");
        json.Append($"\"standard\":{JsonOutput.Quote(config.Project.Standard)},");
        json.Append($"\"linkage\":{JsonOutput.Quote(config.Project.Linkage)},");
        json.Append($"\"installHeaders\":{JsonOutput.Bool(config.Project.InstallHeaders)},");
        json.Append($"\"cmakePolicyVersion\":{JsonOutput.Quote(config.Project.CmakePolicyVersion)},");
        json.Append($"\"testing\":{JsonOutput.Bool(config.Testing)},");
        json.Append($"\"dependencies\":{config.Dependencies.Count},");
        json.Append($"\"conanDependencies\":{config.ConanDependencies.Count},");
        json.Append($"\"scripts\":{config.Scripts.Count},");
        json.Append($"\"resources\":{config.Resources.Files.Count},");
        json.Append($"\"features\":{config.Features.Count}");
        json.Append('}');
        Console.WriteLine(json.ToString());
        return 0;
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
