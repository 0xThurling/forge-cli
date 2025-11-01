using cpm.Models;
using DotMake.CommandLine;
using Spectre.Console;

namespace cpm.Commands
{
  [CliCommand(Name = "create", Description = "Create a new C++ project.", Parent = typeof(RootCommand))]
  public class CreateCommand
  {
    [CliArgument(Description = "The name of the project.")]
    public string Name { get; set; } = string.Empty;

    [CliOption(Description = "Type of project to create (executable or library).")]
    public string Type { get; set; } = "executable";

    public void Run()
    {
      var projectName = Name;
      AnsiConsole.MarkupLine($"[bold cyan]--- Creating project: {projectName} --- [/]");

      try
      {
        // Create directories
        Directory.CreateDirectory(projectName);
        Directory.CreateDirectory(Path.Combine(projectName, "src"));
        Directory.CreateDirectory(Path.Combine(projectName, "assets"));
        Directory.CreateDirectory(Path.Combine(projectName, ".config"));

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
