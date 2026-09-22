using DotMake.CommandLine;
using forge.Models;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Adds or updates a dependency in <c>forge.lua</c>.
  /// </summary>
  /// <example>
  /// <code>
  /// forge add fmt --conan 10.2.1
  /// forge add sdl --git https://github.com/libsdl-org/SDL.git --tag release-2.32.10 --target SDL2::SDL2
  /// forge add forgefp --path ../fp --target forgefp
  /// </code>
  /// </example>
  [CliCommand(Name = "add", Description = "Add or update a dependency in forge.lua.", Parent = typeof(RootCommand))]
  public class AddCommand
  {
    [CliArgument(Description = "Dependency name (the FetchContent name / conan key).")]
    public string Name { get; set; } = null!;

    [CliOption(Description = "Git repository URL", Required = false)]
    public string? Git { get; set; }

    [CliOption(Description = "Git tag, branch or commit (required with --git)", Required = false)]
    public string? Tag { get; set; }

    [CliOption(Description = "Local directory to use instead of git", Required = false)]
    public string? Path { get; set; }

    [CliOption(Description = "Conan package version", Required = false)]
    public string? Conan { get; set; }

    [CliOption(Description = "CMake target to link (defaults to the name)", Required = false)]
    public string? Target { get; set; }

    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      var sources = new[] { Git is not null, Path is not null, Conan is not null }.Count(x => x);
      if (sources != 1)
      {
        AnsiConsole.MarkupLine(
          "[bold red]Error:[/] choose exactly one source: `--git <url> --tag <ref>`, `--path <dir>` or `--conan <version>`.");
        return 1;
      }

      if (Git is not null && string.IsNullOrWhiteSpace(Tag))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `--git` needs `--tag <ref>` (a branch, tag or commit).");
        return 1;
      }

      if (Path is not null && !Directory.Exists(Path))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] no such directory: `{Path}`.");
        return 1;
      }

      if (Conan is not null)
      {
        config.ConanDependencies[Name] = Conan;
        config.Dependencies.Remove(Name);
        ProjectConfigManager.SaveConfig(config);
        AnsiConsole.MarkupLine($"[green]Added[/] {Name} (conan {Conan})");
      }
      else
      {
        config.ConanDependencies.Remove(Name);
        config.Dependencies[Name] = new Dependency
        {
          Git = Git ?? string.Empty,
          Tag = Tag ?? string.Empty,
          Path = Path ?? string.Empty,
          Target = Target ?? string.Empty
        };
        ProjectConfigManager.SaveConfig(config);
        var source = Path is not null ? $"path {Path}" : $"git {Git} @ {Tag}";
        AnsiConsole.MarkupLine($"[green]Added[/] {Name} ({source})");
      }

      AnsiConsole.MarkupLine("[dim]Next: `forge install` to pin it, then `forge build`.[/]");
      return 0;
    }
  }
}
