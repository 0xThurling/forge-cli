using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Rebuilds (or re-tests) the project when its sources change.
/// </summary>
/// <remarks>
/// The first pass runs immediately, then the watched directories are scanned
/// every <c>--interval</c> seconds. A newer, removed or added file triggers the
/// command again. <c>--iterations</c> stops after that many rebuilds, which is
/// what makes the loop usable from a script or a test.
/// </remarks>
/// <example>
/// <code>
/// forge watch                        # rebuild on change
/// forge watch --command test         # re-run the suite on change
/// forge watch --release --interval 0.5
/// forge watch --iterations 2         # build, rebuild once, exit
/// </code>
/// </example>
[CliCommand(Name = "watch", Description = "Rebuild when source files change.", Parent = typeof(RootCommand))]
public class WatchCommand
{
  [CliOption(Description = "Command to run on change: build or test (default: build)", Required = false)]
  public string Command { get; set; } = "build";

  [CliOption(Description = "Seconds between scans (default: 1)", Required = false)]
  public double Interval { get; set; } = 1.0;

  [CliOption(Description = "Stop after this many runs (initial build included)", Required = false)]
  public int? Iterations { get; set; }

  [CliOption(Description = "Directories to watch (default: src, include, test, bench)", Required = false)]
  public string[] Paths { get; set; } = [];

  [CliOption(Description = "Build with the production preset regardless of config.")]
  public bool Release { get; set; }

  [CliOption(Description = "Build with the debug preset regardless of config.")]
  public bool Debug { get; set; }

  [CliOption(Description = "Parallel build jobs (default: all cores)", Required = false)]
  public int? Jobs { get; set; }

  [CliOption(Description = "C++ standard to use. Defaults to the configured standard.", Required = false)]
  public string? Standard { get; set; }

  [CliOption(Description = "Add build presets for a quick build (comma-separated).", Required = false)]
  public string? Preset { get; set; }

  [CliOption(Description = "Ignore presets declared in forge.lua (use only CLI presets).")]
  public bool NoConfigPresets { get; set; }

  public async Task<int> RunAsync()
  {
    if (await ProjectConfigManager.LoadConfigAsync() == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
      return 1;
    }

    var command = Command.Trim().ToLowerInvariant();
    if (command is not ("build" or "test"))
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] unknown command `{Command}` — use `build` or `test`.");
      return 1;
    }

    if (Interval <= 0)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] --interval must be greater than zero.");
      return 1;
    }

    var watched = Paths.Length > 0 ? Paths : ["src", "include", "test", "bench"];
    watched = watched.Where(Directory.Exists).ToArray();
    if (watched.Length == 0)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] nothing to watch — none of src/, include/, test/, bench/ exists.");
      return 1;
    }

    AnsiConsole.MarkupLine(
      $"[dim]Watching {string.Join(", ", watched)} every {Interval:0.##}s — Ctrl+C to stop.[/]");

    // Snapshot before the first build: an edit made *while* it runs should
    // trigger a rebuild afterwards, not be missed by it.
    var snapshot = Snapshot(watched);

    if (await RunOnce(command) != 0)
      return 1;

    // `--iterations` counts runs, not just rebuilds: 1 means "build once and
    // exit", which is what makes the command scriptable.
    var runs = 1;
    var rebuilds = 0;

    while (Iterations is null || runs < Iterations)
    {
      await Task.Delay(TimeSpan.FromSeconds(Interval));
      var current = Snapshot(watched);
      var changes = Changes(snapshot, current);
      snapshot = current;

      if (changes.Count == 0)
        continue;

      runs++;
      rebuilds++;
      AnsiConsole.MarkupLine(
        $"\n[cyan]── change {rebuilds}:[/] {string.Join(", ", changes.Take(3))}" +
        (changes.Count > 3 ? $" (+{changes.Count - 3} more)" : string.Empty));

      if (await RunOnce(command) != 0)
        return 1;
    }

    AnsiConsole.MarkupLine($"[green]Stopped after {rebuilds} rebuild(s).[/]");
    return 0;
  }

  private async Task<int> RunOnce(string command)
  {
    if (command == "test")
    {
      var test = new TestCommand
      {
        Release = Release,
        Debug = Debug,
        Jobs = Jobs,
        Standard = Standard,
        Preset = Preset,
        NoConfigPresets = NoConfigPresets,
      };
      return await test.RunAsync();
    }

    var build = new BuildCommand
    {
      Release = Release,
      Debug = Debug,
      Jobs = Jobs,
      Standard = Standard,
      Preset = Preset,
      NoConfigPresets = NoConfigPresets,
    };
    return await build.RunAsync();
  }

  /// <summary>Every file under the watched directories, with its write time.</summary>
  private static Dictionary<string, DateTime> Snapshot(IEnumerable<string> directories)
  {
    var files = new Dictionary<string, DateTime>(StringComparer.Ordinal);
    foreach (var directory in directories)
    {
      foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
      {
        var parts = file.Split(Path.DirectorySeparatorChar);
        if (parts.Any(part => part is "build" or ".git" || part.StartsWith("build-")))
          continue;

        files[file] = File.GetLastWriteTimeUtc(file);
      }
    }
    return files;
  }

  /// <summary>The paths that were added, removed or rewritten.</summary>
  private static List<string> Changes(
    Dictionary<string, DateTime> before, Dictionary<string, DateTime> after)
  {
    var changes = new List<string>();
    foreach (var (path, stamp) in after)
    {
      if (!before.TryGetValue(path, out var previous) || previous != stamp)
        changes.Add(path);
    }
    foreach (var path in before.Keys)
    {
      if (!after.ContainsKey(path))
        changes.Add(path);
    }
    return changes;
  }
}
