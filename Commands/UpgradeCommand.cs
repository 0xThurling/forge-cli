using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Bumps git dependencies to their newest version-like tag.
/// </summary>
/// <remarks>
/// The report is the default (<c>forge outdated</c> shows the same comparison);
/// <c>--apply</c> writes the new tags to <c>forge.lua</c> and re-pins
/// <c>forge.lock</c> by running the install step, which is what makes the build
/// use the new commits.
/// </remarks>
/// <example>
/// <code>
/// forge upgrade                     # what could be bumped
/// forge upgrade --apply             # write the tags and re-pin the lock
/// forge upgrade fmt --apply         # only this dependency
/// </code>
/// </example>
[CliCommand(Name = "upgrade", Description = "Bump git dependencies to their newest tag.", Parent = typeof(RootCommand))]
public class UpgradeCommand
{
  [CliArgument(Description = "Dependencies to upgrade (default: all).", Required = false)]
  public string[] Dependencies { get; set; } = [];

  [CliOption(Description = "Write the new tags to forge.lua and re-pin forge.lock", Required = false)]
  public bool Apply { get; set; }

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

    var selected = config.Dependencies
      .Where(dep => string.IsNullOrWhiteSpace(dep.Value.Path) &&
                    !string.IsNullOrWhiteSpace(dep.Value.Git) &&
                    !string.IsNullOrWhiteSpace(dep.Value.Tag))
      .ToList();

    if (Dependencies.Length > 0)
    {
      var unknown = Dependencies
        .Where(name => selected.All(dep => !string.Equals(dep.Key, name, StringComparison.OrdinalIgnoreCase)))
        .ToList();
      if (unknown.Count > 0)
      {
        AnsiConsole.MarkupLine(
          $"[bold red]Error:[/] not a git dependency: {string.Join(", ", unknown)}. " +
          $"Git dependencies: {string.Join(", ", selected.Select(dep => dep.Key))}.");
        return 1;
      }

      selected = selected
        .Where(dep => Dependencies.Any(name => string.Equals(dep.Key, name, StringComparison.OrdinalIgnoreCase)))
        .ToList();
    }

    if (selected.Count == 0)
    {
      AnsiConsole.MarkupLine("[yellow]No git dependencies to upgrade.[/]");
      return 0;
    }

    var upgrades = new List<(string Name, string Current, string Latest)>();
    foreach (var (name, dependency) in selected)
    {
      if (!Json)
        AnsiConsole.MarkupLine($"[dim]Checking {name} ({dependency.Tag})…[/]");

      var latest = await GitTags.LatestVersionTagAsync(dependency.Git);
      if (latest is not null && latest != dependency.Tag)
        upgrades.Add((name, dependency.Tag, latest));
    }

    if (Json)
    {
      var sb = new System.Text.StringBuilder("[");
      for (var i = 0; i < upgrades.Count; i++)
      {
        if (i > 0)
          sb.Append(',');
        sb.Append('{');
        sb.Append($"\"name\":{JsonOutput.Quote(upgrades[i].Name)},");
        sb.Append($"\"current\":{JsonOutput.Quote(upgrades[i].Current)},");
        sb.Append($"\"latest\":{JsonOutput.Quote(upgrades[i].Latest)}");
        sb.Append('}');
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
      return 0;
    }

    if (upgrades.Count == 0)
    {
      AnsiConsole.MarkupLine($"[green]All {selected.Count} git dependenc{(selected.Count == 1 ? "y is" : "ies are")} up to date.[/]");
      return 0;
    }

    var table = new Table();
    table.AddColumn("Name");
    table.AddColumn("Current");
    table.AddColumn("Newest");
    foreach (var (name, current, latest) in upgrades)
      table.AddRow(name, current, $"[green]{latest}[/]");
    AnsiConsole.Write(table);

    if (!Apply)
    {
      AnsiConsole.MarkupLine(
        $"[yellow]{upgrades.Count} dependenc{(upgrades.Count == 1 ? "y" : "ies")} could be bumped[/] — " +
        "run `forge upgrade --apply` to write them and re-pin forge.lock.");
      return 0;
    }

    foreach (var (name, _, latest) in upgrades)
      config.Dependencies[name].Tag = latest;

    ProjectConfigManager.SaveConfig(config);
    AnsiConsole.MarkupLine($"[green]Updated {upgrades.Count} tag(s) in forge.lua.[/]");

    // Re-resolve the lock, so the build fetches the new commits.
    var install = new Conan.InstallCommand { Update = true };
    if (await install.RunAsync() != 0)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] forge.lock could not be re-pinned.");
      return 1;
    }

    AnsiConsole.MarkupLine("[green]forge.lock is up to date.[/]");
    return 0;
  }
}
