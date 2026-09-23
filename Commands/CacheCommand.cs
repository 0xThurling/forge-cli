using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Inspects the shared cache of fetched git dependencies.
/// </summary>
/// <remarks>
/// The cache lets a second project — or a second CI run — reuse a dependency
/// checkout instead of cloning it again, and makes builds work offline once it
/// is warm. See <c>forge cache list</c> and <c>forge cache clear</c>.
/// </remarks>
/// <example>
/// <code>
/// forge cache list
/// forge cache clear            # everything
/// forge cache clear fmt        # entries whose name starts with "fmt"
/// </code>
/// </example>
[CliCommand(Name = "cache", Description = "Inspect the shared dependency cache.", Parent = typeof(RootCommand))]
public class CacheCommand { }

/// <summary>Lists the cached dependency checkouts.</summary>
[CliCommand(Name = "list", Description = "List cached dependency checkouts.", Parent = typeof(CacheCommand))]
public class CacheListCommand
{
  [CliOption(Description = "Print JSON", Required = false)]
  public bool Json { get; set; }

  public Task<int> RunAsync()
  {
    var entries = DependencyCache.List();
    var total = entries.Sum(entry => entry.Bytes);

    if (Json)
    {
      var sb = new System.Text.StringBuilder("[");
      for (var i = 0; i < entries.Count; i++)
      {
        if (i > 0)
          sb.Append(',');
        sb.Append('{');
        sb.Append($"\"name\":{JsonOutput.Quote(entries[i].Name)},");
        sb.Append($"\"path\":{JsonOutput.Quote(entries[i].Path)},");
        sb.Append($"\"bytes\":{entries[i].Bytes}");
        sb.Append('}');
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
      return Task.FromResult(0);
    }

    if (entries.Count == 0)
    {
      AnsiConsole.MarkupLine($"[dim]No cached dependencies[/] in {DependencyCache.Root()}");
      return Task.FromResult(0);
    }

    var table = new Table().Title($"[bold]{DependencyCache.Root()}[/]");
    table.AddColumn("Entry");
    table.AddColumn("Size");
    foreach (var (name, _, bytes) in entries)
      table.AddRow(name, $"{bytes / 1024.0 / 1024.0:F1} MiB");
    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine(
      $"[dim]{entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}, {total / 1024.0 / 1024.0:F1} MiB total.[/]");
    return Task.FromResult(0);
  }
}

/// <summary>Removes cached dependency checkouts.</summary>
[CliCommand(Name = "clear", Description = "Remove cached dependency checkouts.", Parent = typeof(CacheCommand))]
public class CacheClearCommand
{
  [CliArgument(Description = "Only entries whose name starts with this.", Required = false)]
  public string? Name { get; set; }

  public Task<int> RunAsync()
  {
    var removed = DependencyCache.Clear(Name);
    AnsiConsole.MarkupLine(removed == 0
      ? $"[dim]Nothing to clear[/] in {DependencyCache.Root()}"
      : $"[green]Removed {removed} cache entr{(removed == 1 ? "y" : "ies")}.[/]");
    return Task.FromResult(0);
  }
}
