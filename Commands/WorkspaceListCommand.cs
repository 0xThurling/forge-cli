using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Lists the workspace projects in build order.
/// </summary>
[CliCommand(Name = "list", Description = "List the workspace projects in build order.", Parent = typeof(WorkspaceCommand))]
public class WorkspaceListCommand
{
  [CliOption(Description = "Print JSON", Required = false)]
  public bool Json { get; set; }

  public async Task<int> RunAsync()
  {
    var root = Workspace.FindRoot();
    if (root == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] No workspace found — run this inside a directory of projects.");
      return 1;
    }

    var projects = await Workspace.DiscoverAsync(root);
    if (projects.Count == 0)
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] No Forge projects found in `{root}`. Add a `forge.workspace.lua` listing them.");
      return 1;
    }

    if (Json)
    {
      var sb = new System.Text.StringBuilder("[");
      for (var i = 0; i < projects.Count; i++)
      {
        var project = projects[i];
        if (i > 0)
          sb.Append(',');
        sb.Append('{');
        sb.Append($"\"name\":{JsonOutput.Quote(project.Name)},");
        sb.Append($"\"type\":{JsonOutput.Quote(project.Type)},");
        sb.Append($"\"version\":{JsonOutput.Quote(project.Version)},");
        sb.Append($"\"path\":{JsonOutput.Quote(Path.GetRelativePath(root, project.Directory))},");
        sb.Append("\"dependsOn\":[");
        for (var d = 0; d < project.DependsOn.Count; d++)
        {
          if (d > 0)
            sb.Append(',');
          sb.Append(JsonOutput.Quote(project.DependsOn[d]));
        }
        sb.Append("]}");
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
      return 0;
    }

    var table = new Table().Title($"[bold]{Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar))}[/]");
    table.AddColumn("Project");
    table.AddColumn("Type");
    table.AddColumn("Version");
    table.AddColumn("Path");
    table.AddColumn("Depends on");

    foreach (var project in projects)
    {
      table.AddRow(
        $"[bold]{project.Name}[/]",
        project.Type,
        project.Version.Length > 0 ? project.Version : "[dim]—[/]",
        Path.GetRelativePath(root, project.Directory),
        project.DependsOn.Count > 0 ? string.Join(", ", project.DependsOn) : "[dim]—[/]");
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine(
      $"[dim]{projects.Count} project(s); build order is the table order.[/]");
    return 0;
  }
}
