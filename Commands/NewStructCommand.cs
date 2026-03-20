using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Generates a new C++ struct with header and optional source file.
  /// </summary>
  /// <remarks>
  /// Creates a new struct with a placeholder for member variables in the header
  /// file. A corresponding source file is also created for any method implementations.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Generate a Vector3 struct
  /// forge new struct Vector3
  /// </code>
  /// </example>
  [CliCommand(Name = "struct", Parent = typeof(NewCommand))]
  public class NewStructCommand
  {
    /// <summary>
    /// Gets or sets the name of the struct to generate.
    /// </summary>
    [CliArgument(Description = "The name of the struct.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Generates the struct header and source files.
    /// </summary>
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
