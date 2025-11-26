using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "scripts", Description = "List all the scripts in the project.", Parent = typeof(ProjectCommand))]
    public class ScriptsCommand
    {
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
