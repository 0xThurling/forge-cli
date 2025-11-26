using cpm.Commands;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "dependencies", Description = "List the project's dependencies and their versions.", Parent = typeof(ProjectCommand))]
    public class DependenciesCommand
    {
        public int Run()
        {
            var config = ProjectConfigManager.LoadConfig();
            if (config == null)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found or is missing project name.");
                return 1;
            }

            if (!config.Dependencies.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No dependencies defined in package.toml.[/]");
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
