using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Removes a dependency from <c>forge.lua</c> (and its lock entry).
  /// </summary>
  [CliCommand(Name = "remove", Description = "Remove a dependency from forge.lua.", Parent = typeof(RootCommand))]
  public class RemoveCommand
  {
    [CliArgument(Description = "Dependency name to remove.")]
    public string Name { get; set; } = null!;

    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      var removed = config.Dependencies.Remove(Name);
      removed |= config.ConanDependencies.Remove(Name);
      if (!removed)
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] no dependency named `{Name}`.");
        return 1;
      }

      ProjectConfigManager.SaveConfig(config);

      // Drop the pin too, so a stale lock entry cannot keep the dependency
      // alive in the generated CMake.
      var lockfile = LockfileManager.Load();
      if (lockfile.Git.Remove(Name))
        LockfileManager.Save(lockfile);

      AnsiConsole.MarkupLine($"[green]Removed[/] {Name}");
      return 0;
    }
  }
}
