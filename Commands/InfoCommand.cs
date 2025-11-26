using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "info", Description = "Display a summary of the project's configuration.", Parent = typeof(ProjectCommand))]
    public class InfoCommand
    {
        public int Run()
        {
            var config = ProjectConfigManager.LoadConfig();
            if (config == null)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found or is missing project name.");
                return 1;
            }

            AnsiConsole.MarkupLine($"[bold]Project Name:[/] {config.Project.Name}");
            AnsiConsole.MarkupLine($"[bold]Project Type:[/] {config.Project.Type}");

            if (config.Dependencies.Any())
            {
                AnsiConsole.MarkupLine("[bold]Dependencies:[/]" + " " + config.Dependencies.Count);
            }

            if (config.Scripts.Any())
            {
                AnsiConsole.MarkupLine("[bold]Scripts:[/]" + " " + config.Scripts.Count);
            }

            return 0;
        }
    }
}
