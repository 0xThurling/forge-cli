using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Lists all project dependencies from forge.lua.
  /// </summary>
  /// <remarks>
  /// Displays a table showing all Git-based dependencies including their names,
  /// repository URLs, and version tags.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project dependencies
  /// </code>
  /// </example>
  [CliCommand(Name = "dependencies", Description = "List the project's dependencies and their versions.", Parent = typeof(ProjectCommand))]
  public class DependenciesCommand
  {
    /// <summary>
    /// Lists all dependencies in a formatted table.
    /// </summary>
    /// <returns>0 on success, 1 if project configuration cannot be loaded.</returns>
    /// <summary>Print machine-readable JSON instead of a table.</summary>
    [CliOption(Description = "Print JSON", Required = false)]
    public bool Json { get; set; }

    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      if (config.Dependencies.Count == 0 && config.ConanDependencies.Count == 0)
      {
        AnsiConsole.MarkupLine("[yellow]No dependencies defined in forge.lua.[/]");
        return 0;
      }

      if (Json)
      {
        var json = new System.Text.StringBuilder();
        json.Append('[');
        var first = true;
        foreach (var dependency in config.Dependencies)
        {
          var details = dependency.Value;
          var isPath = !string.IsNullOrWhiteSpace(details.Path);
          if (!first) json.Append(',');
          first = false;
          json.Append('{');
          json.Append($"\"name\":{JsonOutput.Quote(dependency.Key)},");
          json.Append($"\"channel\":\"{(isPath ? "path" : "git")}\",");
          json.Append($"\"source\":{JsonOutput.Quote(isPath ? details.Path : details.Git)},");
          json.Append($"\"ref\":{JsonOutput.Quote(isPath ? "" : details.Tag)},");
          json.Append($"\"target\":{JsonOutput.Quote(details.Target)}");
          json.Append('}');
        }
        foreach (var package in config.ConanDependencies)
        {
          if (!first) json.Append(',');
          first = false;
          json.Append('{');
          json.Append($"\"name\":{JsonOutput.Quote(package.Key)},");
          json.Append("\"channel\":\"conan\",");
          json.Append($"\"source\":\"conan\",");
          json.Append($"\"ref\":{JsonOutput.Quote(package.Value)},");
          json.Append("\"target\":\"\"");
          json.Append('}');
        }
        json.Append(']');
        Console.WriteLine(json.ToString());
        return 0;
      }

      // One table for every channel: git, local path and Conan, with the
      // source spelled out so a path dependency is not just empty columns.
      var table = new Table();
      table.AddColumn("Name");
      table.AddColumn("Source");
      table.AddColumn("Ref");
      table.AddColumn("Target");

      foreach (var dependency in config.Dependencies)
      {
        var details = dependency.Value;
        var source = string.IsNullOrWhiteSpace(details.Path)
          ? details.Git
          : $"path:{details.Path}";
        var reference = string.IsNullOrWhiteSpace(details.Path) ? details.Tag : "";
        table.AddRow(dependency.Key, source, reference, details.Target);
      }

      foreach (var package in config.ConanDependencies)
      {
        table.AddRow(package.Key, "conan", package.Value, "");
      }

      AnsiConsole.Write(table);

      return 0;
    }
  }
}
