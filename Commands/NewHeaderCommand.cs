using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Generates a new C++ header file with include guards.
  /// </summary>
  /// <remarks>
  /// Creates a minimal header file with include guards and a placeholder for code.
  /// Only the header file is created - no source file is generated.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Generate a utilities header
  /// forge new header Utils
  /// // Results in: src/Utils.h
  /// </code>
  /// </example>
  [CliCommand(Name = "header", Parent = typeof(NewCommand))]
  public class NewHeaderCommand
  {
    /// <summary>
    /// Gets or sets the name of the header file to create (without extension).
    /// </summary>
    [CliArgument(Description = "The name of the header file (without extension).")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Generates the header file.
    /// </summary>
    public async Task Run()
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
