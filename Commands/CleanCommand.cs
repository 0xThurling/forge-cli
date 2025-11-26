using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "clean", Description = "Remove the build directory.", Parent = typeof(RootCommand))]
    public class CleanCommand
    {
        public void Run()
        {
            var buildDir = "build";
            if (Directory.Exists(buildDir))
            {
                AnsiConsole.MarkupLine($"[bold cyan]--- Removing build directory: {buildDir} ---[/]");
                try
                {
                    Directory.Delete(buildDir, true);
                    AnsiConsole.MarkupLine("[bold green]Project cleaned.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Build directory not found. Nothing to clean.[/]");
            }
        }
    }
}
