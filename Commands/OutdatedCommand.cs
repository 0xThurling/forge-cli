using System.Diagnostics;
using System.Text;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Reports git dependencies whose declared tag is behind the newest tag in the
/// repository.
/// </summary>
/// <remarks>
/// Compares the declared ref with the newest semver-like tag found with
/// <c>git ls-remote --tags</c>. Conan and vcpkg dependencies are not checked
/// (their versioning is not tag-based); local <c>path</c> dependencies have
/// nothing to compare.
/// </remarks>
[CliCommand(Name = "outdated", Description = "Show git dependencies that are behind their newest tag.", Parent = typeof(RootCommand))]
public class OutdatedCommand
{
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

    var outdated = new List<(string Name, string Current, string Latest)>();
    var checkedCount = 0;

    foreach (var (name, dependency) in config.Dependencies)
    {
      if (!string.IsNullOrWhiteSpace(dependency.Path) ||
          string.IsNullOrWhiteSpace(dependency.Git) ||
          string.IsNullOrWhiteSpace(dependency.Tag))
      {
        continue;
      }

      checkedCount++;
      var latest = await LatestTagAsync(dependency.Git);
      if (latest is not null && latest != dependency.Tag)
        outdated.Add((name, dependency.Tag, latest));
    }

    if (Json)
    {
      var sb = new StringBuilder("[");
      for (var i = 0; i < outdated.Count; i++)
      {
        if (i > 0) sb.Append(',');
        sb.Append('{');
        sb.Append($"\"name\":{JsonOutput.Quote(outdated[i].Name)},");
        sb.Append($"\"current\":{JsonOutput.Quote(outdated[i].Current)},");
        sb.Append($"\"latest\":{JsonOutput.Quote(outdated[i].Latest)}");
        sb.Append('}');
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
      return 0;
    }

    if (checkedCount == 0)
    {
      AnsiConsole.MarkupLine("[yellow]No git dependencies to check.[/]");
      return 0;
    }

    if (outdated.Count == 0)
    {
      AnsiConsole.MarkupLine($"[green]All {checkedCount} git dependenc{(checkedCount == 1 ? "y is" : "ies are")} up to date.[/]");
      return 0;
    }

    var table = new Table();
    table.AddColumn("Name");
    table.AddColumn("Declared");
    table.AddColumn("Newest tag");
    foreach (var (name, current, latest) in outdated)
      table.AddRow(name, current, $"[green]{latest}[/]");
    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine("[dim]Update the tag in forge.lua, then run `forge install` to re-pin it.[/]");
    return 0;
  }

  /// <summary>The newest semver-like tag in a repository, or null.</summary>
  private static async Task<string?> LatestTagAsync(string repository)
  {
    try
    {
      var psi = new ProcessStartInfo("git", $"ls-remote --tags \"{repository}\"")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      using var process = Process.Start(psi);
      if (process == null)
        return null;

      var output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();
      if (process.ExitCode != 0)
        return null;

      var tags = output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('\t'))
        .Where(parts => parts.Length == 2 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal))
        .Select(parts => parts[1]["refs/tags/".Length..].Trim())
        .Where(tag => !tag.EndsWith("^{}", StringComparison.Ordinal))
        .Where(IsVersionLike)
        .ToList();

      return tags.Count == 0 ? null : tags.OrderBy(tag => tag, VersionComparer).Last();
    }
    catch (Exception)
    {
      return null;
    }
  }

  private static bool IsVersionLike(string tag) =>
    tag.TrimStart('v', 'V').Split('.').All(part => part.Length > 0 && part.All(char.IsAsciiDigit));

  /// <summary>Compares version-like tags numerically (v1.10 &gt; v1.9).</summary>
  private static readonly IComparer<string> VersionComparer = Comparer<string>.Create((a, b) =>
  {
    var left = a.TrimStart('v', 'V').Split('.').Select(int.Parse).ToArray();
    var right = b.TrimStart('v', 'V').Split('.').Select(int.Parse).ToArray();
    for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
    {
      var l = i < left.Length ? left[i] : 0;
      var r = i < right.Length ? right[i] : 0;
      if (l != r)
        return l.CompareTo(r);
    }
    return 0;
  });
}
