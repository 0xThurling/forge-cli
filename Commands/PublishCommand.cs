using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Packages the project with CPack into a distribution directory.
/// </summary>
/// <remarks>
/// Requires <c>project.version</c>: it names the archive and is what
/// <c>CPackConfig.cmake</c> is generated from. Forge writes that config during
/// <c>forge build</c> (see the packaging section), so publishing without a
/// build either builds first or fails with a clear message.
/// </remarks>
/// <example>
/// <code>
/// forge publish                                  # dist/&lt;name&gt;-&lt;version&gt;-Linux.tar.gz
/// forge publish --format ZIP
/// forge publish --format DEB --output out
/// </code>
/// </example>
[CliCommand(Name = "publish", Description = "Package the project with CPack.", Parent = typeof(RootCommand))]
public class PublishCommand
{
  [CliOption(Description = "CPack generator: TGZ, ZIP, DEB, RPM, … (default: TGZ)", Required = false)]
  public string Format { get; set; } = "TGZ";

  [CliOption(Description = "Output directory (default: dist)", Required = false)]
  public string Output { get; set; } = "dist";

  [CliOption(Description = "Package the existing build instead of building first", Required = false)]
  public bool NoBuild { get; set; }

  [CliOption(Description = "Configuration to package with a multi-config generator (e.g. Release)", Required = false)]
  public string? Config { get; set; }

  public async Task<int> RunAsync()
  {
    var config = await ProjectConfigManager.LoadConfigAsync();
    if (config == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
      return 1;
    }

    if (string.IsNullOrWhiteSpace(config.Project.Version))
    {
      AnsiConsole.MarkupLine(
        "[bold red]Error:[/] `project.version` is required to publish — set it in forge.lua.");
      return 1;
    }

    if (!NoBuild)
    {
      var build = new BuildCommand();
      if (await build.RunAsync() != 0)
        return 1;
    }

    var cpackConfig = Path.Combine("build", "CPackConfig.cmake");
    if (!File.Exists(cpackConfig))
    {
      AnsiConsole.MarkupLine(
        $"[bold red]Error:[/] `{cpackConfig}` not found — run `forge build` first " +
        "(it writes the CPack configuration).");
      return 1;
    }

    Directory.CreateDirectory(Output);
    // Republishing overwrites the same archive, so compare timestamps rather
    // than just looking for files that did not exist before.
    var before = Directory.GetFiles(Output)
      .ToDictionary(file => file, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

    var arguments = new List<string> { "--config", cpackConfig, "-G", Format, "-B", Output };
    if (!string.IsNullOrWhiteSpace(Config))
    {
      arguments.Add("-C");
      arguments.Add(Config);
    }

    var psi = new ProcessStartInfo("cpack")
    {
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in arguments)
      psi.ArgumentList.Add(argument);

    using var process = Process.Start(psi);
    if (process == null)
    {
      AnsiConsole.MarkupLine("[bold red]Error:[/] could not start `cpack` (it ships with CMake).");
      return 1;
    }

    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
      AnsiConsole.MarkupLine($"[bold red]cpack failed[/] (exit {process.ExitCode}).");
      return process.ExitCode;
    }

    var produced = Directory.GetFiles(Output)
      .Where(file => !before.TryGetValue(file, out var stamp) || File.GetLastWriteTimeUtc(file) > stamp)
      .OrderBy(file => file)
      .ToList();

    // cpack succeeded but nothing changed: report what is there.
    if (produced.Count == 0)
      produced = Directory.GetFiles(Output).OrderBy(file => file).ToList();

    if (produced.Count == 0)
    {
      AnsiConsole.MarkupLine(
        "[yellow]cpack produced nothing[/] — does the project install any files? " +
        "Libraries install with `project.version`; executables are packaged to `bin/`.");
      return 0;
    }

    foreach (var file in produced)
      AnsiConsole.MarkupLine($"[green]📦 {file}[/] [dim]({new FileInfo(file).Length / 1024} KiB)[/]");
    return 0;
  }
}
