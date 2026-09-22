using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Shared plumbing for the workspace commands that run a Forge command in each
/// project, in dependency order.
/// </summary>
internal static class WorkspaceRunner
{
  /// <summary>
  /// Loads the workspace and returns the projects to act on (all of them, or
  /// the selected ones plus their local dependencies), in build order.
  /// </summary>
  public static async Task<(string Root, List<WorkspaceProject> Projects)?> PlanAsync(string[] selected)
  {
    var root = Workspace.FindRoot();
    if (root == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] No workspace found — run this inside a directory of projects.");
      return null;
    }

    var ordered = await Workspace.DiscoverAsync(root);
    if (ordered.Count == 0)
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] No Forge projects found in `{root}`. Add a `forge.workspace.lua` listing them.");
      return null;
    }

    if (selected.Length == 0)
      return (root, ordered);

    var byName = ordered.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
    var unknown = selected.Where(name => !byName.ContainsKey(name)).ToList();
    if (unknown.Count > 0)
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] unknown project(s): {string.Join(", ", unknown)}. " +
        $"Known: {string.Join(", ", ordered.Select(project => project.Name))}.");
      return null;
    }

    // A selected project pulls in its local dependencies: building half of a
    // dependency chain would fail anyway.
    var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Add(WorkspaceProject project)
    {
      if (!wanted.Add(project.Name))
        return;
      foreach (var dependency in project.DependsOn)
      {
        if (byName.TryGetValue(dependency, out var target))
          Add(target);
      }
    }

    foreach (var name in selected)
      Add(byName[name]);

    return (root, ordered.Where(project => wanted.Contains(project.Name)).ToList());
  }

  /// <summary>
  /// Runs <paramref name="verb"/> in each project. The first failure stops the
  /// run, because later projects usually depend on the failed one.
  /// </summary>
  public static async Task<int> RunAsync(
    List<WorkspaceProject> projects,
    string verb,
    IReadOnlyList<string> extraArguments,
    bool dryRun)
  {
    if (dryRun)
    {
      AnsiConsole.MarkupLine($"[dim]Would run `forge {verb}` in:[/]");
      foreach (var project in projects)
        AnsiConsole.MarkupLine($"   {project.Name} [dim]({project.Directory})[/]");
      return 0;
    }

    foreach (var project in projects)
    {
      AnsiConsole.MarkupLine($"\n[bold cyan]══ {project.Name} ({verb}) ══[/]");

      var arguments = new List<string> { verb };
      arguments.AddRange(extraArguments);

      var exit = await SelfInvocation.RunAsync(project.Directory, arguments);
      if (exit != 0)
      {
        AnsiConsole.MarkupLine($"\n[bold red]`forge {verb}` failed in {project.Name}[/] (exit {exit}).");
        return exit;
      }
    }

    AnsiConsole.MarkupLine($"\n[green]`forge {verb}` succeeded in {projects.Count} project(s).[/]");
    return 0;
  }
}
