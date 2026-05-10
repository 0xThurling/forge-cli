using DotMake.CommandLine;
using forge.Commands.Conan;
using Spectre.Console;

namespace forge.Commands;

[CliCommand(Name = "doctor", Description = "Analyze project configuration and report issues.", Parent = typeof(RootCommand))]
public class DoctorCommand
{
  private readonly ConanDependencyChecker _conanChecker = new();

  public async Task<int> RunAsync()
  {
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
      var icon = exists ? "[green]✅[/]" : (required ? "[red]❌[/]" : "[yellow]⚠️[/]");
      var status = exists ? "exists" : (required ? "missing" : "missing (optional)");
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

      // Check if build scripts exist
      if (Directory.Exists(".config/forge/build"))
      {
        var scripts = Directory.GetFiles(".config/forge/build", "*.lua");
        AnsiConsole.MarkupLine($"   [dim]Build scripts found: {scripts.Length}[/]");
      }
    }
    else
    {
      AnsiConsole.MarkupLine("   [dim]No custom scripts configured[/]");
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
}
