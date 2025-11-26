using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "class", Parent = typeof(NewCommand))]
  public class NewClassCommand
  {
    [CliArgument(Description = "The name of the class.")]
    public string Name { get; set; } = string.Empty;

    public void Run()
    {
      var className = Name;
      if (!Directory.Exists("src"))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `src` directory not found. Please run this command from the project root.");
        return;
      }

      var headerContent = $$"""
#ifndef {{className.ToUpper()}}_H
#define {{className.ToUpper()}}_H

class {{className}} {
public:
    {{className}}();
    ~{{className}}();
};

#endif // {{className.ToUpper()}}_H
""";

      var cppContent = $$"""
#include "{{className}}.h"

{{className}}::{{className}}() {
    // Constructor implementation
}

{{className}}::~{{className}}() {
    // Destructor implementation
}
""";

      var headerPath = Path.Combine("src", $"{className}.h");
      var cppPath = Path.Combine("src", $"{className}.cpp");

      if (File.Exists(headerPath) || File.Exists(cppPath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Class `[bold]{className}[/]` already exists.");
        return;
      }

      try
      {
        File.WriteAllText(headerPath, headerContent);
        File.WriteAllText(cppPath, cppContent);
        AnsiConsole.MarkupLine($"[bold green]Created class `[bold]{className}[/]` in `src/`.[/]");
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
      }
    }
  }
}
