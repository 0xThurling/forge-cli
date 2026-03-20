using DotMake.CommandLine;
using forge.Commands.Lua;
using forge.Models;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Creates a new C++ project with a standard directory structure and initial configuration.
  /// </summary>
  /// <remarks>
  /// This command initializes a new Forge project by creating the required directory
  /// structure, generating a package.toml configuration file, and setting up Lua
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
    /// The name used for the project directory, executable/library, and package.toml name.
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

    /// <summary>
    /// Executes the project creation process.
    /// </summary>
    /// <remarks>
    /// Creates the following structure:
    /// - src/ containing main.cpp (executable) or empty (library)
    /// - external/ for Git dependencies
    /// - assets/ for resource files
    /// - .config/forge/ containing Lua configuration directories
    /// - package.toml with project configuration
    /// - .gitignore with appropriate patterns
    /// </remarks>
    public void Run()
    {
      var projectName = Name;
      AnsiConsole.MarkupLine($"[bold cyan]--- Creating project: {projectName} --- [/]");

      try
      {
        // Create directories
        Directory.CreateDirectory(projectName);
        Directory.CreateDirectory(Path.Combine(projectName, "src"));
        Directory.CreateDirectory(Path.Combine(projectName, "external"));
        Directory.CreateDirectory(Path.Combine(projectName, "assets"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config"));

        // Create Lua directories
        Directory.CreateDirectory(Path.Combine(projectName, ".config", "forge"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config", "forge", "commands"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config", "forge", "build"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config", "forge", "templates"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config", "forge", "definitions"));

        // Initial Lua definitions
        LuaEngine.SetEnvironmentDefinitions(projectName);

        // Create src/main.cpp for executable
        if (Type == "executable")
        {
          var mainCppContent = "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, C++ World!\" << std::endl;\n    return 0;\n}";
          File.WriteAllText(Path.Combine(projectName, "src", "main.cpp"), mainCppContent);
        }

        // Create package.toml
        var projectConfig = new ProjectConfig
        {
          Project = new ProjectSection
          {
            Name = projectName,
            Type = Type
          },
        };

        if (Type == "library")
        {
          projectConfig.Project.InstallHeaders = true;
        }

        ProjectConfigManager.SaveConfig(projectConfig, projectName);

        // Create a placeholder .gitignore
        var gitignoreContent = "build/\nlib/\ncompile_commands.json\n.config/\nconanfile.txt\n";
        File.WriteAllText(Path.Combine(projectName, ".gitignore"), gitignoreContent);

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
