using forge.Models;
using Spectre.Console;

namespace forge;

/// <summary>
/// The closing report of a library build: the artifact a library target
/// produced, or why there is nothing to link. Shared by <c>forge build</c> and
/// <c>forge run</c> (a library is not runnable).
/// </summary>
internal static class LibraryArtifact
{
  /// <summary>The file names a library target can produce.</summary>
  private static string[] Names(string name) =>
    [$"lib{name}.a", $"{name}.lib", $"lib{name}.so", $"lib{name}.dylib", $"{name}.dll"];

  /// <summary>
  /// Reports a library project's build. Always returns 0: the build itself
  /// already succeeded, and a source-less (INTERFACE) library has no artifact
  /// by design.
  /// </summary>
  public static int Report(ProjectConfig config)
  {
    var found = Find(config.Project.Name);
    if (found is not null)
    {
      var file = new FileInfo(found);
      AnsiConsole.MarkupLine("[green]Library built successfully![/]");
      AnsiConsole.MarkupLine($"   Path: {found}");
      AnsiConsole.MarkupLine($"   Size: {file.Length / 1024.0:F2} KB");
      AnsiConsole.WriteLine();
      AnsiConsole.MarkupLine("[yellow]Note:[/] Libraries cannot be executed directly.");
      AnsiConsole.MarkupLine("[dim]To use this library, add it as a dependency in another project or include headers from src/[/]");
      return 0;
    }

    if (!SourceFiles.HasSources())
    {
      // No sources means an INTERFACE target: nothing is built, and that is
      // not a failure.
      AnsiConsole.MarkupLine("[bold green]Build finished successfully.[/]");
      AnsiConsole.MarkupLine(
        "[yellow]Note:[/] Header-only library: there is no artifact to link; dependents include the headers directly.");
      return 0;
    }

    AnsiConsole.MarkupLine("[bold red]Error:[/] Library output not found in build/ directory.");
    AnsiConsole.MarkupLine(
      $"[dim]Expected: {string.Join(", ", Names(config.Project.Name).Select(name => $"build/{name}"))}[/]");
    return 0;
  }

  /// <summary>
  /// The first matching artifact under <c>build/</c>, configuration
  /// subdirectories included — a multi-config generator puts them in
  /// <c>build/&lt;Config&gt;/</c> — or null.
  /// </summary>
  private static string? Find(string name)
  {
    if (!Directory.Exists("build"))
      return null;

    var wanted = Names(name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var path in Directory.EnumerateFiles("build", "*", SearchOption.AllDirectories))
    {
      if (path.Split(Path.DirectorySeparatorChar).Contains("CMakeFiles"))
        continue;
      if (wanted.Contains(Path.GetFileName(path)))
        return path;
    }

    return null;
  }
}
