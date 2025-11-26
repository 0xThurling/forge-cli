using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "source", Parent = typeof(NewCommand))]
  public class NewSourceCommand
  {
    [CliArgument(Description = "The name of the source files (without extension).")]
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
      var cppContent = $"""
#include "{fileName}.h"

// Your code here
""";

      var headerPath = Path.Combine("src", $"{fileName}.h");
      var cppPath = Path.Combine("src", $"{fileName}.cpp");

      if (File.Exists(headerPath) || File.Exists(cppPath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Source files `[bold]{fileName}[/]` already exist.");
        return;
      }

      try
      {
        File.WriteAllText(headerPath, headerContent);
        File.WriteAllText(cppPath, cppContent);
        AnsiConsole.MarkupLine($"[bold green]Created source files `[bold]{fileName}.h[/]` and `[bold]{fileName}.cpp[/]` in `src/`.[/]");
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
      }
    }
  }
}
