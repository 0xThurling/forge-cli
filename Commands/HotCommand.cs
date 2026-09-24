using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Builds the project for hot reload and runs it, reloading when sources change.
/// </summary>
/// <remarks>
/// The build is a debug one — the engine patches unoptimised, unstripped code —
/// with the pinned jet-live dependency wired in. A save is signalled to the
/// running process, which keeps its state: only function bodies are replaced.
/// A file added or removed rebuilds first, because the generated compile
/// commands change.
/// </remarks>
/// <example>
/// <code>
/// forge hot                       # build, run, reload on save
/// forge hot --manual              # reload only when signalled
/// forge hot -- --input data.txt   # arguments for the program
/// </code>
/// </example>
[CliCommand(Name = "hot", Description = "Build and run with hot reload (edit code while it runs).", Parent = typeof(RootCommand))]
public class HotCommand
{
  [CliOption(Description = "Reload only when signalled (no reload on save).", Required = false)]
  public bool Manual { get; set; }

  [CliOption(Description = "Seconds between file scans (default: 0.5)", Required = false)]
  public double Interval { get; set; } = 0.5;

  [CliOption(Description = "Skip the initial build and run the existing binary.", Required = false)]
  public bool NoBuild { get; set; }

  [CliOption(Description = "Executable target to run (default: the project's own).", Required = false)]
  public string? Bin { get; set; }

  [CliOption(Description = "Arguments for the program (after `--`).", Required = false)]
  public string[] Arguments { get; set; } = [];

  public async Task<int> RunAsync()
  {
    if (OperatingSystem.IsWindows())
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] hot reload is not supported on Windows (the engine is Linux/macOS only).");
      return 1;
    }

    if (Interval <= 0)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] --interval must be greater than zero.");
      return 1;
    }

    if (!NoBuild)
    {
      var build = new BuildCommand { Hot = true, Debug = true };
      if (await build.RunAsync() != 0)
        return 1;
    }

    var config = await ProjectConfigManager.LoadConfigAsync();
    if (config == null)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
      return 1;
    }

    var wanted = string.IsNullOrWhiteSpace(Bin) ? config.Project.Name : Bin!;
    var executablePath = StartCommand.FindExecutable(wanted);
    if (string.IsNullOrEmpty(executablePath))
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] executable `{wanted}` not found — build it first or drop `--no-build`.");
      return 1;
    }

    var startInfo = new ProcessStartInfo(executablePath) { UseShellExecute = false };
    foreach (var argument in Arguments)
      startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo);
    if (process == null)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] could not start `{executablePath}`.");
      return 1;
    }

    Console.CancelKeyPress += (_, eventArgs) =>
    {
      eventArgs.Cancel = true;
      try
      {
        process.Kill(entireProcessTree: true);
      }
      catch
      {
        // Already gone.
      }
    };

    AnsiConsole.MarkupLine($"[cyan]Hot reload running:[/] {executablePath} (pid {process.Id})");

    if (Manual)
    {
      AnsiConsole.MarkupLine($"[dim]Reload with `kill -USR1 {process.Id}` — Ctrl+C to stop.[/]");
      process.WaitForExit();
      return process.ExitCode;
    }

    var watched = new[] { "src", "include", "test", "bench" }.Where(Directory.Exists).ToArray();
    if (watched.Length == 0)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] nothing to watch — none of src/, include/, test/, bench/ exists.");
      try
      {
        process.Kill(entireProcessTree: true);
      }
      catch
      {
        // Already gone.
      }

      return 1;
    }

    AnsiConsole.MarkupLine($"[dim]Watching {string.Join(", ", watched)} — Ctrl+C to stop.[/]");

    var snapshot = FileSnapshot.Take(watched);
    while (!process.HasExited)
    {
      await Task.Delay(TimeSpan.FromSeconds(Interval));

      var current = FileSnapshot.Take(watched);
      var changes = FileSnapshot.Diff(snapshot, current);
      snapshot = current;

      if (changes.Count == 0)
        continue;

      var paths = changes.Paths.ToList();
      AnsiConsole.MarkupLine(
        $"\n[cyan]── change:[/] {string.Join(", ", paths.Take(3))}" +
        (paths.Count > 3 ? $" (+{paths.Count - 3} more)" : string.Empty));

      if (changes.IsStructural)
      {
        // A new or removed file changes the generated compile commands, so the
        // build has to run again before the engine can see it.
        var rebuild = new BuildCommand { Hot = true, Debug = true };
        if (await rebuild.RunAsync() != 0)
          AnsiConsole.MarkupLine("[yellow]Build failed — the running code stays.[/]");

        // The build itself may rewrite generated sources (embedded resources);
        // those are not user edits and must not trigger another cycle.
        snapshot = FileSnapshot.Take(watched);
      }

      if (process.HasExited)
        break;

      if (!HotReload.SendReloadSignal(process.Id))
        AnsiConsole.MarkupLine("[yellow]Could not signal the process — is it still running?[/]");
    }

    return process.ExitCode;
  }
}
