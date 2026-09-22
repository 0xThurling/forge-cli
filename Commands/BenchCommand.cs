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

    AnsiConsole.MarkupLine($"[dim]Running {binary}[/]");
    var psi = new ProcessStartInfo(Path.GetFullPath(binary))
    {
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in Arguments)
      psi.ArgumentList.Add(argument);

    using var process = Process.Start(psi);
    if (process == null)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] could not start `{binary}`.");
      return 1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
  }

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
