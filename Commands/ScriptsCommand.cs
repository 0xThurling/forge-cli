using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Lists all custom scripts defined in forge.lua.
  /// </summary>
  /// <remarks>
  /// Displays all scripts available in the [scripts] section of forge.lua.
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
    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      // Project-local commands count as scripts, so a project that only uses
      // .config/forge/commands/ must still be listed.
      var projectCommands = ProjectCommands.Names().ToList();
      if (config?.Scripts == null || (config.Scripts.Count == 0 && projectCommands.Count == 0))
      {
        AnsiConsole.MarkupLine("[yellow]No scripts defined in forge.lua.[/]");
        return 0;
      }

      AnsiConsole.MarkupLine("");
      var root = new Tree("Available Scripts: ");
      foreach (var script in config.Scripts)
      {
        root.AddNode($"[green]{script.Key}[/]");
      }

      // Project-local commands behave like scripts; list them too, marked so
      // the difference is visible.
      foreach (var name in projectCommands)
      {
        if (!config.Scripts.ContainsKey(name))
          root.AddNode($"[green]{name}[/] [dim]({ProjectCommands.Directory})[/]");
      }
      AnsiConsole.Write(root);
      AnsiConsole.MarkupLine("");
      return 0;
    }
  }
}
