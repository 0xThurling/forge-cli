using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "header", Parent = typeof(NewCommand))]
  public class NewHeaderCommand
  {
    [CliArgument(Description = "The name of the header file (without extension).")]
    public string Name { get; set; } = string.Empty;

    public void Run()
    {
      var fileName = Name;
      if (!Directory.Exists("src"))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `src` directory not found. Please run this command from the project root.");
        return;
      }

      var headerContent = $"""
#ifndef {fileName.ToUpper()}_H
#define {fileName.ToUpper()}_H

// Your code here

#endif // {fileName.ToUpper()}_H
""";

      var headerPath = Path.Combine("src", $"{fileName}.h");

      if (File.Exists(headerPath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Header file `[bold]{fileName}.h[/]` already exists.");
        return;
      }

      try
      {
        File.WriteAllText(headerPath, headerContent);
        AnsiConsole.MarkupLine($"[bold green]Created header file `[bold]{fileName}.h[/]` in `src/`.[/]");
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
      }
    }
  }
}
