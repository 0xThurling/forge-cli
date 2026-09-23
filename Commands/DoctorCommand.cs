using DotMake.CommandLine;
using forge.Commands.Conan;
using forge.Commands.Lua;
using Spectre.Console;

namespace forge.Commands;

[CliCommand(Name = "doctor", Description = "Analyze project configuration and report issues.", Parent = typeof(RootCommand))]
public class DoctorCommand
{
  private readonly ConanDependencyChecker _conanChecker = new();

  /// <summary>
  /// The layout Forge expects, shared by the human report and the JSON one so
  /// they cannot disagree about what is missing.
  /// </summary>
  private static readonly (string Path, bool Required, string Note)[] LayoutEntries =
  [
    ("src", false, "Source files"),
    ("external", false, "External dependencies"),
    ("assets", false, "Resource files"),
    (".config/forge", true, "Forge configuration"),
    (".config/forge/build", false, "Build scripts"),
  ];

  /// <summary>Print a machine-readable report instead of the human one.</summary>
  [CliOption(Description = "Print JSON (and exit non-zero when unhealthy)", Required = false)]
  public bool Json { get; set; }

  /// <summary>Create what is missing instead of only reporting it.</summary>
  [CliOption(Description = "Create missing directories and files", Required = false)]
  public bool Fix { get; set; }

  public async Task<int> RunAsync()
  {
    // JSON first: a consumer parsing stdout must not have to skip a banner.
    if (Json)
      return await RunJsonAsync();

    AnsiConsole.MarkupLine("[bold cyan]🔍 Running Forge Doctor...[/]");
    AnsiConsole.WriteLine();

    var issues = 0;
    var config = await ProjectConfigManager.LoadConfigAsync();

    // Config file check
    AnsiConsole.MarkupLine("[bold]1. Configuration file: [/]");
    if (config != null)
    {
      AnsiConsole.MarkupLine("   [green]✅ forge.lua loaded successfully[/]");

      if (!string.IsNullOrEmpty(config.Project.Name))
      {
        AnsiConsole.MarkupLine($"   [green]✅ Project name:[/] {config.Project.Name}");
      }
      if (!string.IsNullOrEmpty(config.Project.Type))
      {
        AnsiConsole.MarkupLine($"   [green]✅ Project type:[/] {config.Project.Type}");
      }
    }
    else
    {
      AnsiConsole.MarkupLine("   [red]❌ No forge.lua found in current directory[/]");
      issues++;
    }
    AnsiConsole.WriteLine();

    // Directory structure
    AnsiConsole.MarkupLine("[bold]2. Directory Structure[/]");
    var dirs = new (string path, bool required, string note)[]
          {
            ("src", false, "Source files"),
            ("external", false, "External dependencies"),
            ("assets", false, "Resource files"),
            (".config/forge", true, "Forge configuration"),
            (".config/forge/build", false, "Build scripts"),
          };

    foreach (var (path, required, note) in dirs)
    {
      var exists = Directory.Exists(path);
      var status = exists ? "exists" : (required ? "missing" : "missing (optional)");

      if (!exists && Fix)
      {
        try
        {
          Directory.CreateDirectory(path);
          status = "created";
          exists = true;
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"   [red]❌[/] {path}/ - {note} (could not create: {ex.Message})");
          issues++;
          continue;
        }
      }

      var icon = exists ? "[green]✅[/]" : (required ? "[red]❌[/]" : "[yellow]⚠️[/]");
      AnsiConsole.MarkupLine($"   {icon} {path}/ - {note} ({status})");
    }
    AnsiConsole.WriteLine();

    // 3. Dependency check
    AnsiConsole.MarkupLine("[bold]3. Dependencies:[/]");
    if (config != null)
    {
      // Git Dependencies
      if (config.Dependencies.Count > 0)
      {
        AnsiConsole.MarkupLine($"   [green]✅ Git dependencies:[/] {string.Join(", ", config.Dependencies.Keys)}");

        // Check if external directories exist
        foreach (var dep in config.Dependencies.Keys)
        {
          var depPath = Path.Combine("external", dep);
          var depExists = Directory.Exists(depPath);
          var icon = depExists ? "[green]✅[/]" : "[yellow]⚠️[/]";
          AnsiConsole.MarkupLine($"      {icon} {dep}/ - {(depExists ? "fetched" : "not fetched yet")}");
        }
      }
      else
      {
        AnsiConsole.MarkupLine("   [dim]No git dependencies configured[/]");
      }

      // Conan Dependencies
      if (config.ConanDependencies.Count > 0)
      {
        AnsiConsole.MarkupLine($"   [green]✅ Conan dependencies:[/] {string.Join(", ", config.ConanDependencies.Keys)}");

        // Check for Conan lock file
        var lockExists = File.Exists("conan.lock");
        var icon = lockExists ? "[green]✅[/]" : "[yellow]⚠️[/]";
        AnsiConsole.MarkupLine($"      {icon} conan.lock - {(lockExists ? "installed" : "not installed yet - run 'forge install'")}");
      }
      else
      {
        AnsiConsole.MarkupLine("   [dim]No Conan dependencies configured[/]");
      }
      AnsiConsole.WriteLine();

      // 4. Check for dependency conflicts
      AnsiConsole.MarkupLine("[bold]4. Dependency conflicts:[/]");
      if (config.Dependencies.Count > 0 || config.ConanDependencies.Count > 0)
      {
        await _conanChecker.CheckForConflicts(config);
      }
      else
      {
        AnsiConsole.MarkupLine("   [dim]No dependencies to check[/]");
      }
      AnsiConsole.WriteLine();
    }

    // 5. Resource Files Check
    AnsiConsole.MarkupLine("[bold]5. Resource Files:[/]");

    if (config?.Resources?.Files?.Count > 0)
    {
      foreach (var resource in config.Resources.Files)
      {
        var exists = File.Exists(resource);
        var icon = exists ? "[green]✅[/]" : "[red]❌[/]";
        AnsiConsole.MarkupLine($"   {icon} {resource}");
      }
    }
    else
    {
      AnsiConsole.MarkupLine("   [dim]No resources configured[/]");
    }
    AnsiConsole.WriteLine();

    // 6. Scripts Check
    AnsiConsole.MarkupLine("[bold]6. Scripts:[/]");

    if (config?.Scripts?.Count > 0)
    {
      foreach (var (name, cmd) in config.Scripts)
      {
        AnsiConsole.MarkupLine($"   [green]✅[/] {name} = {cmd}");
      }

    }
    else
    {
      AnsiConsole.MarkupLine("   [dim]No custom scripts configured[/]");
    }

    // Build scripts are independent of the custom-script list.
    if (Directory.Exists(".config/forge/build"))
    {
      var buildScripts = Directory.GetFiles(".config/forge/build", "*.lua");
      AnsiConsole.MarkupLine($"   [dim]Build scripts found: {buildScripts.Length}[/]");
    }
    AnsiConsole.WriteLine();

    // 7. Features Check
    AnsiConsole.MarkupLine("[bold]7. Features:[/]");

    if (config?.Features?.Count > 0)
    {
      foreach (var (name, feature) in config.Features)
      {
        var status = feature.Enabled ? "[green]enabled[/]" : "[dim]disabled[/]";
        AnsiConsole.MarkupLine($"   - {name}: {status}");

        if (feature.Options.Count > 0)
        {
          foreach (var (optKey, optVal) in feature.Options)
          {
            AnsiConsole.MarkupLine($"      - {optKey} = {optVal}");
          }
        }
      }
    }
    else
    {
      AnsiConsole.MarkupLine("   [dim]No features configured[/]");
    }
    AnsiConsole.WriteLine();

    // 8. Generated files vs .gitignore
    AnsiConsole.MarkupLine("[bold]8. Generated files:[/]");
    var ignorePath = ".gitignore";
    var ignoreText = File.Exists(ignorePath) ? File.ReadAllText(ignorePath) : string.Empty;
    var ignoreLines = ignoreText
      .Split('\n', StringSplitOptions.RemoveEmptyEntries)
      .Select(line => line.Trim())
      .ToHashSet(StringComparer.Ordinal);
    var missingIgnores = SourceFiles.GeneratedIgnoreEntries
      .Where(entry => !ignoreLines.Contains(entry))
      .ToList();

    if (missingIgnores.Count == 0)
    {
      AnsiConsole.MarkupLine("   [green]✅[/] .gitignore covers the generated files");
    }
    else
    {
      AnsiConsole.MarkupLine(
        $"   [yellow]⚠️[/] .gitignore does not ignore: {string.Join(", ", missingIgnores)}");

      if (Fix)
      {
        if (!File.Exists(ignorePath))
          File.WriteAllText(ignorePath, SourceFiles.DefaultGitIgnore());
        else
          File.AppendAllText(
            ignorePath,
            (ignoreText.EndsWith('\n') || ignoreText.Length == 0 ? string.Empty : "\n") +
            string.Join("\n", missingIgnores) + "\n");
        AnsiConsole.MarkupLine("   [green]✅[/] added the missing entries");

        // The editor stubs track the installed CLI, so refresh them here too.
        LuaEngine.WriteEnvironmentDefinitions(".");
        AnsiConsole.MarkupLine("   [green]✅[/] refreshed .config/forge/definitions/definitions.lua");
      }
      else
      {
        AnsiConsole.MarkupLine("   [dim]Run `forge doctor --fix` to add them.[/]");
      }
    }
    AnsiConsole.WriteLine();

    // 9. Toolchain
    AnsiConsole.MarkupLine("[bold]9. Toolchain:[/]");
    var missingTools = new List<string>();
    foreach (var tool in ToolRequirements.All)
    {
      var version = tool.Detect();
      var acceptable = tool.IsVersionAcceptable(version);
      if (!acceptable)
      {
        missingTools.Add(tool.Name);
        if (tool.Required)
          issues++;
      }

      var icon = acceptable ? "[green]✅[/]" : (tool.Required ? "[red]❌[/]" : "[yellow]⚠️[/]");
      var detail = version ?? "not found";
      if (version is not null && !acceptable)
        detail = $"{version} (needs {tool.MinimumVersion})";
      AnsiConsole.MarkupLine($"   {icon} {tool.Name} - {detail}");
    }

    // A tool that is installed but not on PATH cannot be used, and a generator
    // is not optional.
    foreach (var (name, reason, fix) in ToolRequirements.NeedsRepair())
    {
      AnsiConsole.MarkupLine($"   [red]❌[/] {name} {reason}");
      AnsiConsole.MarkupLine($"      [dim]fix:[/] {fix}");
    }

    foreach (var (name, location, fix) in ToolRequirements.PathProblems())
    {
      AnsiConsole.MarkupLine($"   [yellow]⚠️[/] {name} is at {location}, which is not on PATH");
      AnsiConsole.MarkupLine($"      [dim]fix:[/] {fix}");
    }

    if (ToolLocator.Find("ninja") is null && ToolLocator.Find("make") is null)
    {
      AnsiConsole.MarkupLine("   [red]❌[/] neither ninja nor make is installed — CMake needs a generator");
      issues++;
    }

    if (missingTools.Count > 0)
    {
      AnsiConsole.MarkupLine(
        $"[dim]Run `forge setup` to see how to install: {string.Join(", ", missingTools)}[/]");
    }
    AnsiConsole.WriteLine();

    // Summary
    AnsiConsole.MarkupLine("[bold]Summary:[/]");
    if (issues == 0)
    {
      AnsiConsole.MarkupLine("   [green]✅ Project looks healthy![/]");
    }
    else
    {
      AnsiConsole.MarkupLine($"   [yellow]⚠️  Found {issues} issue(s) to address[/]");
    }
    return 0;
  }

  /// <summary>
  /// The same checks as the human report, as JSON. Kept deliberately separate:
  /// the human path is a long narrative, and threading a serializer through it
  /// would make both harder to change. The facts are shared — the layout table,
  /// the ignore entries and the tool table.
  /// </summary>
  private async Task<int> RunJsonAsync()
  {
    var config = await ProjectConfigManager.LoadConfigAsync();

    var missingRequired = LayoutEntries
      .Where(entry => entry.Required && !Directory.Exists(entry.Path))
      .Select(entry => entry.Path)
      .ToList();
    var missingOptional = LayoutEntries
      .Where(entry => !entry.Required && !Directory.Exists(entry.Path))
      .Select(entry => entry.Path)
      .ToList();

    var ignoreLines = File.Exists(".gitignore")
      ? File.ReadAllLines(".gitignore").Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal)
      : new HashSet<string>(StringComparer.Ordinal);
    var missingIgnores = SourceFiles.GeneratedIgnoreEntries
      .Where(entry => !ignoreLines.Contains(entry))
      .ToList();

    var toolchain = ToolRequirements.All
      .Select(tool =>
      {
        var version = tool.Detect();
        return (Tool: tool, Version: version, Ok: tool.IsVersionAcceptable(version));
      })
      .ToList();
    var missingRequiredTools = toolchain.Count(entry => entry.Tool.Required && !entry.Ok);
    var issues = missingRequired.Count + missingRequiredTools;

    var sb = new System.Text.StringBuilder();
    sb.Append('{');
    sb.Append($"\"healthy\":{JsonOutput.Bool(issues == 0)},");
    sb.Append($"\"issues\":{issues},");
    sb.Append("\"project\":{");
    sb.Append($"\"name\":{JsonOutput.Quote(config?.Project.Name ?? string.Empty)},");
    sb.Append($"\"type\":{JsonOutput.Quote(config?.Project.Type ?? string.Empty)},");
    sb.Append($"\"standard\":{JsonOutput.Quote(config?.Project.Standard ?? string.Empty)}}},");
    sb.Append("\"layout\":{");
    sb.Append($"\"missingRequired\":[{string.Join(",", missingRequired.Select(JsonOutput.Quote))}],");
    sb.Append($"\"missingOptional\":[{string.Join(",", missingOptional.Select(JsonOutput.Quote))}]}},");
    sb.Append($"\"gitignore\":{{\"missing\":[{string.Join(",", missingIgnores.Select(JsonOutput.Quote))}]}},");
    sb.Append("\"toolchain\":[");
    for (var i = 0; i < toolchain.Count; i++)
    {
      if (i > 0)
        sb.Append(',');
      var (tool, version, ok) = toolchain[i];
      sb.Append('{');
      sb.Append($"\"tool\":{JsonOutput.Quote(tool.Name)},");
      sb.Append($"\"installed\":{JsonOutput.Bool(version is not null)},");
      sb.Append($"\"version\":{JsonOutput.Quote(version ?? string.Empty)},");
      sb.Append($"\"required\":{JsonOutput.Bool(tool.Required)},");
      sb.Append($"\"ok\":{JsonOutput.Bool(ok)}");
      sb.Append('}');
    }
    sb.Append("],");
    sb.Append("\"dependencies\":{");
    sb.Append($"\"git\":{config?.Dependencies.Count ?? 0},");
    sb.Append($"\"conan\":{config?.ConanDependencies.Count ?? 0},");
    sb.Append($"\"vcpkg\":{config?.VcpkgDependencies.Count ?? 0},");
    sb.Append($"\"pkgconfig\":{config?.PkgConfigDependencies.Count ?? 0}");
    sb.Append("}}");
    Console.WriteLine(sb.ToString());

    return issues == 0 ? 0 : 1;
  }
}
