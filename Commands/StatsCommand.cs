using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Displays statistics about the project including file counts and sizes.
  /// </summary>
  /// <remarks>
  /// Counts all files in the project directory and calculates total lines of code
  /// and total file size. Build artifacts are included in the count.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project stats
  /// </code>
  /// </example>
  [CliCommand(Name = "stats", Description = "Display some statistics about the project.", Parent = typeof(ProjectCommand))]
  public class StatsCommand
  {
    /// <summary>
    /// Calculates and displays project statistics.
    /// </summary>
    /// <returns>0 on success, 1 if project root cannot be found.</returns>
    public async Task<int> Run()
    {
      var projectRoot = ProjectConfigManager.FindProjectRoot();
      if (projectRoot == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found.");
        return 1;
      }

      var files = Directory.GetFiles(projectRoot, "*.*", SearchOption.AllDirectories);
      var totalFiles = files.Length;
      var totalLines = 0;
      long totalSize = 0;

      foreach (var file in files)
      {
        try
        {
          totalLines += File.ReadLines(file).Count();
          totalSize += new FileInfo(file).Length;
        }
        catch
        {
          // ignored
        }
      }

      AnsiConsole.MarkupLine($"[bold]Total Files:[/] {totalFiles}");
      AnsiConsole.MarkupLine($"[bold]Total Lines:[/] {totalLines}");
      AnsiConsole.MarkupLine($"[bold]Total Size:[/] {totalSize / 1024} KB");

      return 0;
    }
  }
}
