using DotMake.CommandLine;
using forge.Commands.Conan;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Explains where a dependency comes from: declared in <c>forge.lua</c>, or
/// pulled in by another Conan package.
/// </summary>
/// <remarks>
/// Direct dependencies are read from the configuration. For transitive ones the
/// resolved graph (<c>conan graph info</c>) is walked upwards, which is the only
/// place that information exists.
/// </remarks>
/// <example>
/// <code>
/// forge why fmt          # direct? or which conan package needs it
/// forge why fmt --json
/// </code>
/// </example>
[CliCommand(Name = "why", Description = "Explain why a dependency is in the project.", Parent = typeof(RootCommand))]
public class WhyCommand
{
  [CliArgument(Description = "Dependency (or package) name.")]
  public string Dependency { get; set; } = string.Empty;

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

    var name = Dependency.Trim();
    if (name.Length == 0)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] which dependency? `forge why <name>`.");
      return 1;
    }

    var (channel, detail) = Direct(config, name);

    // Conan's graph is the only source for transitive packages.
    var packages = channel is null && config.ConanDependencies.Count > 0
      ? await ConanGraph.LoadAsync(Path.Combine(".config", "conanfile.txt"))
      : null;

    ConanPackage? package = null;
    var chain = new List<ConanPackage>();
    if (packages is not null)
    {
      package = ConanGraph.Find(packages, name);
      if (package is not null)
        chain = ConanGraph.Explain(packages, package);
    }

    if (channel is null && package is null)
    {
      if (Json)
      {
        Console.WriteLine($"{{\"name\":{JsonOutput.Quote(name)},\"found\":false}}");
      }
      else
      {
        AnsiConsole.MarkupLine($"[yellow]{name}[/] is not declared in forge.lua" +
          (packages is null
            ? " (and there is no Conan graph to search — run `forge install`)."
            : ", nor in the resolved Conan graph."));
      }
      return 1;
    }

    if (Json)
    {
      var sb = new System.Text.StringBuilder();
      sb.Append('{');
      sb.Append($"\"name\":{JsonOutput.Quote(name)},");
      sb.Append($"\"found\":true,");
      sb.Append($"\"channel\":{JsonOutput.Quote(channel ?? "conan-transitive")},");
      sb.Append($"\"detail\":{JsonOutput.Quote(detail ?? package?.Reference ?? string.Empty)},");
      sb.Append("\"requiredBy\":[");
      for (var i = 0; i < chain.Count; i++)
      {
        if (i > 0)
          sb.Append(',');
        sb.Append(JsonOutput.Quote(chain[i].Reference.Length > 0 ? chain[i].Reference : chain[i].Name));
      }
      sb.Append("]}");
      Console.WriteLine(sb.ToString());
      return 0;
    }

    AnsiConsole.MarkupLine($"[bold]{name}[/]");
    if (channel is not null)
    {
      AnsiConsole.MarkupLine($"   [green]direct dependency[/] [dim]({channel}{detail})[/]");
      return 0;
    }

    AnsiConsole.MarkupLine($"   [cyan]transitive[/] [dim]{package!.Reference}[/]");
    for (var i = 1; i < chain.Count; i++)
    {
      var parent = chain[i];
      var isDirect = config.ConanDependencies.ContainsKey(parent.Name);
      AnsiConsole.MarkupLine(
        $"   required by [bold]{parent.Name}[/] {parent.Version}" +
        (isDirect ? " [green](direct dependency)[/]" : string.Empty));
    }

    if (chain.Count == 1)
      AnsiConsole.MarkupLine("   [dim]no requiring package found in the graph[/]");

    return 0;
  }

  /// <summary>
  /// The channel a name is declared in, with a short detail string, or
  /// (null, null) when it is not a direct dependency.
  /// </summary>
  private static (string? Channel, string? Detail) Direct(Models.ProjectConfig config, string name)
  {
    foreach (var (key, dependency) in config.Dependencies)
    {
      if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
        continue;

      if (!string.IsNullOrWhiteSpace(dependency.Path))
        return ("path", $", path = {dependency.Path}");
      if (!string.IsNullOrWhiteSpace(dependency.Git))
        return ("git", $", {dependency.Git} @ {dependency.Tag}");
      return ("git", string.Empty);
    }

    if (config.ConanDependencies.TryGetValue(name, out var conanVersion))
      return ("conan", $", version {conanVersion}");

    if (config.VcpkgDependencies.ContainsKey(name))
      return ("vcpkg", string.Empty);

    if (config.PkgConfigDependencies.Any(module => string.Equals(module, name, StringComparison.OrdinalIgnoreCase)))
      return ("pkgconfig", string.Empty);

    return (null, null);
  }
}
