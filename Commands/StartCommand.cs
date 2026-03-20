using System.Diagnostics;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Builds and runs the project's executable.
  /// </summary>
  /// <remarks>
  /// This command is invoked when running `forge run` without arguments.
  /// It first builds the project using BuildCommand, then locates and executes
  /// the built executable.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Build and run the project
  /// forge run
  /// </code>
  /// </example>
  public class StartCommand
  {
    /// <summary>
    /// Builds the project and executes the resulting binary.
    /// </summary>
    /// <returns>0 on success, non-zero on failure.</returns>
    public int Run()
    {
      var buildCommand = new BuildCommand();
      if (buildCommand.Run() != 0)
      {
        return 1;
      }

      var projectName = ProjectConfigManager.GetProjectName();
      if (string.IsNullOrEmpty(projectName))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Could not find project name to run.");
        return 1;
      }

      var executablePath = Path.Combine("build", projectName);
      if (!File.Exists(executablePath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Executable not found at '[bold]{executablePath}[/]'.");
        return 1;
      }

      try
      {
        var processStartInfo = new ProcessStartInfo(executablePath)
        {
          UseShellExecute = false,
          RedirectStandardOutput = false,
          RedirectStandardError = false,
          CreateNoWindow = true,
        };

        using (var process = Process.Start(processStartInfo))
        {
          if (process == null) throw new Exception("Failed to start program process.");
          process.WaitForExit();
          return process.ExitCode;
        }
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        return 1;
      }
    }
  }
}
