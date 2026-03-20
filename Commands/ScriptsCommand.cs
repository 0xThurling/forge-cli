using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Lists all custom scripts defined in package.toml.
  /// </summary>
  /// <remarks>
  /// Displays all scripts available in the [scripts] section of package.toml.
  /// These can be executed using the `forge run <script-name>` command.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project scripts
  /// </code>
  /// </example>
  [CliCommand(Name = "scripts", Description = "List all the scripts in the project.", Parent = typeof(ProjectCommand))]
  public class ScriptsCommand
  {
    /// <summary>
    /// Lists all available scripts.
    /// </summary>
    /// <returns>Always returns 0.</returns>
    public int Run()
    {
      var config = ProjectConfigManager.LoadConfig();
      if (config?.Scripts == null || !config.Scripts.Any())
      {
        AnsiConsole.MarkupLine("[yellow]No scripts defined in package.toml.[/]");
        return 0;
      }

      AnsiConsole.MarkupLine("");
      var root = new Tree("Available Scripts: ");
      foreach (var script in config.Scripts)
      {
        root.AddNode($"[green]{script.Key}[/]");
      }
      AnsiConsole.Write(root);
      AnsiConsole.MarkupLine("");
      return 0;
    }
  }
}
