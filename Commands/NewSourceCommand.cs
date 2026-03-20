using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Generates a new C++ header and source file pair.
  /// </summary>
  /// <remarks>
  /// Creates both a header file with include guards and a corresponding source file
  /// that includes the header. This is useful for creating utility functions or
  /// free functions that don't require a class.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Generate MathUtils header and source
  /// forge new source MathUtils
  /// // Results in: src/MathUtils.h and src/MathUtils.cpp
  /// </code>
  /// </example>
  [CliCommand(Name = "source", Parent = typeof(NewCommand))]
  public class NewSourceCommand
  {
    /// <summary>
    /// Gets or sets the name of the source files to create (without extension).
    /// </summary>
    [CliArgument(Description = "The name of the source files (without extension).")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Generates the header and source files.
    /// </summary>
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
