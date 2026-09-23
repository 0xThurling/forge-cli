using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Removes the build directory to clean the project.
  /// </summary>
  /// <remarks>
  /// Deletes the build/ directory and all its contents, effectively resetting
  /// the project to a clean state. This is useful for forcing a full rebuild
  /// or cleaning up disk space.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Clean the build directory
  /// forge clean
  /// </code>
  /// </example>
  [CliCommand(Name = "clean", Description = "Remove the build directory.", Parent = typeof(RootCommand))]
  public class CleanCommand
  {
    /// <summary>
    /// Removes the build directory and all its contents.
    /// </summary>
    public async Task RunAsync()
    {
      var buildDir = "build";
      if (Directory.Exists(buildDir))
      {
        AnsiConsole.MarkupLine($"[bold cyan]--- Removing build directory: {buildDir} ---[/]");
        try
        {
          Directory.Delete(buildDir, true);

          // The compile database is a symlink into build/: leaving it behind
          // means a dangling link that editors keep trying to read.
          var compileCommands = "compile_commands.json";
          if (File.Exists(compileCommands) &&
              new FileInfo(compileCommands).LinkTarget is not null)
          {
            File.Delete(compileCommands);
            AnsiConsole.MarkupLine($"[dim]Removed the {compileCommands} symlink.[/]");
          }

          AnsiConsole.MarkupLine("[bold green]Project cleaned.[/]");
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
      }
      else
      {
        AnsiConsole.MarkupLine("[yellow]Build directory not found. Nothing to clean.[/]");
      }
    }
  }
}
