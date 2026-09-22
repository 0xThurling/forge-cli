using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Copies fetched git dependencies into the project and switches them to local
/// <c>path</c> dependencies, so the project builds without network access.
/// </summary>
/// <remarks>
/// The sources come from FetchContent's checkout in <c>build/_deps/&lt;name&gt;-src</c>,
/// so run <c>forge build</c> (or <c>forge install</c>) first. The lock entries
/// for vendored dependencies are dropped — they are local now.
/// </remarks>
[CliCommand(Name = "vendor", Description = "Copy fetched git dependencies into external/ for offline builds.", Parent = typeof(RootCommand))]
public class VendorCommand
{
  [CliOption(Description = "Directory to vendor into (default: external)", Required = false)]
  public string Directory { get; set; } = "external";

  public async Task<int> RunAsync()
  {
    var config = await ProjectConfigManager.LoadConfigAsync();
    if (config == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
      return 1;
    }

    var vendored = 0;
    var missing = 0;

    foreach (var (name, dependency) in config.Dependencies)
    {
      if (!string.IsNullOrWhiteSpace(dependency.Path) ||
          string.IsNullOrWhiteSpace(dependency.Git))
      {
        continue; // already local
      }

      var source = Path.Combine("build", "_deps", name + "-src");
      if (!System.IO.Directory.Exists(source))
      {
        AnsiConsole.MarkupLine(
          $"[yellow]Skipping[/] {name}: {source} not found — run `forge build` first.");
        missing++;
        continue;
      }

      var destination = Path.Combine(Directory, name).Replace('\\', '/');
      if (System.IO.Directory.Exists(destination))
        System.IO.Directory.Delete(destination, recursive: true);

      CopyDirectory(source, destination);
      AnsiConsole.MarkupLine($"[green]Vendored[/] {name} → {destination}");

      dependency.Path = destination;
      dependency.Git = string.Empty;
      dependency.Tag = string.Empty;
      vendored++;
    }

    if (vendored == 0)
    {
      AnsiConsole.MarkupLine(missing > 0
        ? "[yellow]Nothing vendored.[/]"
        : "[yellow]No git dependencies to vendor.[/]");
      return 0;
    }

    ProjectConfigManager.SaveConfig(config);

    var lockfile = LockfileManager.Load();
    var dropped = 0;
    foreach (var name in config.Dependencies.Keys)
    {
      if (lockfile.Git.Remove(name))
        dropped++;
    }
    if (dropped > 0)
      LockfileManager.Save(lockfile);

    AnsiConsole.MarkupLine(
      $"[green]Vendored {vendored} dependenc{(vendored == 1 ? "y" : "ies")}[/] into `{Directory}`" +
      (dropped > 0 ? $" (dropped {dropped} lock entr{(dropped == 1 ? "y" : "ies")})" : "") +
      ". Builds no longer need the network.");
    return 0;
  }

  /// <summary>
  /// Copies a directory tree, skipping the VCS metadata (a vendored copy is a
  /// snapshot, not a checkout).
  /// </summary>
  private static void CopyDirectory(string source, string destination)
  {
    System.IO.Directory.CreateDirectory(destination);

    foreach (var file in System.IO.Directory.GetFiles(source))
      File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

    foreach (var directory in System.IO.Directory.GetDirectories(source))
    {
      if (Path.GetFileName(directory) is ".git" or ".cache")
        continue;
      CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
  }
}
