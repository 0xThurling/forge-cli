using DotMake.CommandLine;
using forge.Models;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "embed", Description = "Embed a resource file into a C++ header.", Parent = typeof(RootCommand))]
    public class EmbedCommand
    {
        [CliArgument(Description = "The path to the resource file to embed.")]
        public string FilePath { get; set; } = string.Empty;

        public void Run()
        {
            if (!File.Exists(FilePath))
            {
                AnsiConsole.MarkupLine($"[bold red]Error:[/] File not found at '[bold]{FilePath}[/]'.");
                return;
            }

            AnsiConsole.MarkupLine($"[bold cyan]--- Registering resource: {FilePath} ---[/]");

            var config = ProjectConfigManager.LoadConfig();
            if (config == null)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] `package.toml` not found.");
                return;
            }

            // Use relative path for portability
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), FilePath);

            if (!config.Resources.Files.Contains(relativePath))
            {
                config.Resources.Files.Add(relativePath);
                try
                {
                    ProjectConfigManager.SaveConfig(config);
                    AnsiConsole.MarkupLine($"[bold green]Successfully registered `[bold]{relativePath}[/]` in package.toml.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[bold red]Error:[/] Could not write to package.toml: {ex.Message}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Resource `[bold]{relativePath}[/]` is already registered in package.toml.[/]");
            }
        }
    }
}
