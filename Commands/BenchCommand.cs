using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Builds and runs the project's benchmark target.
/// </summary>
/// <remarks>
/// Requires <c>testing = { benchmark = true }</c> and sources under
/// <c>bench/</c>; the target is <c>&lt;project&gt;_bench</c>. Extra arguments
/// are passed through to the benchmark binary (Google Benchmark's own flags,
/// e.g. <c>--benchmark_filter</c>).
/// </remarks>
[CliCommand(Name = "bench", Description = "Build and run the project's benchmarks.", Parent = typeof(RootCommand))]
public class BenchCommand
{
  [CliArgument(Description = "Arguments passed to the benchmark binary.", Required = false)]
  public string[] Arguments { get; set; } = [];

  [CliOption(Description = "Skip the build and run the existing binary", Required = false)]
  public bool NoBuild { get; set; }

  [CliOption(Description = "Write this run's results as JSON (a baseline)", Required = false)]
  public string? Save { get; set; }

  [CliOption(Description = "Compare against a baseline written by --save", Required = false)]
  public string? Compare { get; set; }

  [CliOption(Description = "Fail when a benchmark is slower than the baseline by more than this percent", Required = false)]
  public double? FailOver { get; set; }

  [CliOption(Description = "Print the comparison as JSON", Required = false)]
  public bool Json { get; set; }

  public async Task<int> RunAsync()
  {
    var config = await ProjectConfigManager.LoadConfigAsync();
    if (config == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
      return 1;
    }

    if (!config.Benchmark)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] benchmarks are not enabled. Add `testing = { benchmark = true }` and put sources in `bench/`.");
      return 1;
    }

    if (!NoBuild)
    {
      var buildCommand = new BuildCommand();
      if (await buildCommand.RunAsync() != 0)
        return 1;
    }

    var binary = LocateBinary(config.Project.Name);
    if (binary is null)
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] `{config.Project.Name}_bench` not found in build/ — is there a source in `bench/`?");
      return 1;
    }

    // A run that has to be measured needs its results somewhere: `--save` says
    // where, `--compare` needs one too (a temporary file when only comparing).
    var runReport = Save
      ?? (Compare is not null ? Path.Combine(Path.GetTempPath(), $"forge-bench-{Environment.ProcessId}.json") : null);

    var arguments = new List<string>(Arguments);
    if (runReport is not null &&
        !arguments.Any(argument => argument.StartsWith("--benchmark_out", StringComparison.Ordinal)))
    {
      arguments.Add($"--benchmark_out={Path.GetFullPath(runReport)}");
      arguments.Add("--benchmark_format=json");
    }

    AnsiConsole.MarkupLine($"[dim]Running {binary}[/]");
    var psi = new ProcessStartInfo(Path.GetFullPath(binary))
    {
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in arguments)
      psi.ArgumentList.Add(argument);

    using var process = Process.Start(psi);
    if (process == null)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] could not start `{binary}`.");
      return 1;
    }

    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
      return process.ExitCode;

    if (Save is not null)
      AnsiConsole.MarkupLine($"[green]Saved[/] {Save}");

    if (Compare is null)
      return 0;

    return CompareWith(Compare, runReport!);
  }

  /// <summary>
  /// Compares a run against a baseline and reports the deltas. A regression
  /// beyond <c>--fail-over</c> fails the command, which is what makes this
  /// usable as a performance gate.
  /// </summary>
  private int CompareWith(string baselinePath, string currentPath)
  {
    var baseline = BenchmarkReport.Read(baselinePath);
    var current = BenchmarkReport.Read(currentPath);
    if (baseline.Count == 0 || current.Count == 0)
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] could not read the benchmark results — run `forge bench --save <file>` first.");
      return 1;
    }

    var rows = new List<(string Name, double Baseline, double Current, double Change, string Unit)>();
    foreach (var (key, now) in current)
    {
      if (!baseline.TryGetValue(key, out var before))
        continue;

      // Normalise to nanoseconds: Google Benchmark picks a unit per benchmark,
      // and the two runs need not have chosen the same one.
      var beforeNs = before.Nanoseconds;
      var nowNs = now.Nanoseconds;
      var change = beforeNs == 0 ? 0 : (nowNs - beforeNs) / beforeNs * 100.0;
      rows.Add((key, beforeNs, nowNs, change, "ns"));
    }

    var newOnes = current.Keys.Where(key => !baseline.ContainsKey(key)).ToList();
    var gone = baseline.Keys.Where(key => !current.ContainsKey(key)).ToList();

    if (Json)
    {
      var sb = new System.Text.StringBuilder("[");
      for (var i = 0; i < rows.Count; i++)
      {
        if (i > 0)
          sb.Append(',');
        var (name, before, now, change, _) = rows[i];
        sb.Append('{');
        sb.Append($"\"name\":{JsonOutput.Quote(name)},");
        sb.Append($"\"baselineNs\":{before.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},");
        sb.Append($"\"currentNs\":{now.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},");
        sb.Append($"\"changePercent\":{change.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
        sb.Append('}');
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
    }
    else
    {
      var table = new Table().Title($"[bold]vs {baselinePath}[/]");
      table.AddColumn("Benchmark");
      table.AddColumn("Baseline");
      table.AddColumn("Now");
      table.AddColumn("Change");
      foreach (var (name, before, now, change, _) in rows.OrderByDescending(row => row.Change))
      {
        var colour = change > 0 ? "red" : "green";
        table.AddRow(
          name,
          Format(before),
          Format(now),
          $"[{colour}]{change:+0.0;-0.0;0.0}%[/]");
      }
      AnsiConsole.Write(table);
    }

    if (newOnes.Count > 0)
      AnsiConsole.MarkupLine($"[dim]new: {string.Join(", ", newOnes)}[/]");
    if (gone.Count > 0)
      AnsiConsole.MarkupLine($"[dim]gone: {string.Join(", ", gone)}[/]");

    var regressions = rows.Where(row => row.Change > (FailOver ?? double.MaxValue)).ToList();
    if (FailOver.HasValue && regressions.Count > 0)
    {
      AnsiConsole.MarkupLine(
        $"[bold red]{regressions.Count} benchmark(s) slower than {FailOver:0.#}%[/]: " +
        string.Join(", ", regressions.Select(row => $"{row.Name} ({row.Change:+0.0}%)")));
      return 1;
    }

    return 0;
  }

  /// <summary>Nanoseconds, in a readable unit.</summary>
  private static string Format(double nanoseconds) => nanoseconds switch
  {
    >= 1_000_000_000 => $"{nanoseconds / 1_000_000_000:0.###} s",
    >= 1_000_000 => $"{nanoseconds / 1_000_000:0.###} ms",
    >= 1_000 => $"{nanoseconds / 1_000:0.###} us",
    _ => $"{nanoseconds:0.##} ns"
  };

  private static string? LocateBinary(string projectName)
  {
    var candidates = new[]
    {
      Path.Combine("build", projectName + "_bench"),
      Path.Combine("build", "bench", projectName + "_bench"),
      Path.Combine("build", "Release", projectName + "_bench"),
      Path.Combine("build", projectName + "_bench.exe")
    };
    return candidates.FirstOrDefault(File.Exists);
  }
}
