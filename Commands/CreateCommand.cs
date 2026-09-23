using DotMake.CommandLine;
using forge.Commands.Lua;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Creates a new C++ project with a standard directory structure and initial configuration.
  /// </summary>
  /// <remarks>
  /// This command initializes a new Forge project by creating the required directory
  /// structure, generating a forge.lua configuration file, and setting up Lua
  /// environment definitions. The created project is immediately ready for use with
  /// standard CMake-based C++ development.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Create an executable project
  /// forge create mygame
  /// 
  /// // Create a library project
  /// forge create mylibrary --type library
  /// </code>
  /// </example>
  [CliCommand(Name = "create", Description = "Create a new C++ project.", Parent = typeof(RootCommand))]
  public class CreateCommand
  {
    /// <summary>
    /// Gets or sets the name of the project to create.
    /// </summary>
    /// <value>
    /// The name used for the project directory, executable/library, and forge.lua name.
    /// </value>
    [CliArgument(Description = "The name of the project.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of project to create.
    /// </summary>
    /// <value>
    /// "executable" creates a runnable application, "library" creates a static library.
    /// Defaults to "executable".
    /// </value>
    [CliOption(Description = "Type of project to create (executable or library).")]
    public string Type { get; set; } = "executable";

    [CliOption(Description = "C++ standard (default: 20)", Required = false)]
    public string Standard { get; set; } = "20";

    [CliOption(Description = "Enable the test target", Required = false)]
    public bool Testing { get; set; }

    /// <summary>
    /// Executes the project creation process.
    /// </summary>
    /// <remarks>
    /// Creates the following structure:
    /// - src/ containing main.cpp (executable) or empty (library)
    /// - external/ for Git dependencies
    /// - assets/ for resource files
    /// - .config/forge/ containing Lua configuration directories
    /// - forge.lua with project configuration
    /// - .gitignore with appropriate patterns
    /// </remarks>
    public async Task RunAsync()
    {
      var projectName = Name;
      var type = Type.Trim().ToLowerInvariant();
      if (type is not ("executable" or "library"))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] unknown type `{Type}` — use `executable` or `library`.");
        return;
      }

      AnsiConsole.MarkupLine($"[bold cyan]--- Creating project: {projectName} --- [/]");

      try
      {
        ProjectScaffolder.Scaffold(projectName, projectName, type, Standard, Testing);

        AnsiConsole.MarkupLine($"[bold green]Successfully created project `[bold yellow]{projectName}[/]`.[/]");
        AnsiConsole.MarkupLine($"To get started, `cd [bold yellow]{projectName}[/]`.");
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
      }
    }
  }
}
