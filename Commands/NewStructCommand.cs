using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "struct", Parent = typeof(NewCommand))]
  public class NewStructCommand
  {
    [CliArgument(Description = "The name of the struct.")]
    public string Name { get; set; } = string.Empty;

    public void Run()
    {
      var structName = Name;
      if (!Directory.Exists("src"))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `src` directory not found. Please run this command from the project root.");
        return;
      }

      var headerContent = $"#ifndef {structName.ToUpper()}_H\n#define {structName.ToUpper()}_H\n\nstruct {structName} {{\n    // struct members\n}};\n\n#endif // {structName.ToUpper()}_H";
      var cppContent = $"#include \"{structName}.h\"\n\n// Implementation for struct methods if any";

      var headerPath = Path.Combine("src", $"{structName}.h");
      var cppPath = Path.Combine("src", $"{structName}.cpp");

      if (File.Exists(headerPath) || File.Exists(cppPath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Struct `[bold]{structName}[/]` already exists.");
        return;
      }

      try
      {
        File.WriteAllText(headerPath, headerContent);
        File.WriteAllText(cppPath, cppContent);
        AnsiConsole.MarkupLine($"[bold green]Created struct `[bold]{structName}[/]` in `src/`.[/]");
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
      }
    }
  }
}
