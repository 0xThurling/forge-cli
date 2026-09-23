using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Checks the tools Forge drives, and installs what is missing.
/// </summary>
/// <remarks>
/// The check is read-only and is the default: it prints what exists, what is
/// missing, and the exact command that would install it. <c>--install</c> runs
/// those commands (asking first, unless <c>--yes</c>), using the machine's own
/// package manager and its own sudo prompt — Forge never handles a password.
/// </remarks>
/// <example>
/// <code>
/// forge setup                      # what is installed, what is missing
/// forge setup --install            # install the missing tools (asks first)
/// forge setup --install --dry-run  # show the commands, change nothing
/// forge setup --tools cmake,ninja
/// </code>
/// </example>
[CliCommand(Name = "setup", Description = "Check (or install) the tools Forge works best with.", Parent = typeof(RootCommand))]
public class SetupCommand
{
  [CliOption(Description = "Install what is missing", Required = false)]
  public bool Install { get; set; }

  [CliOption(Description = "Skip the confirmation prompt", Required = false)]
  public bool Yes { get; set; }

  [CliOption(Description = "Print the commands instead of running them", Required = false)]
  public bool DryRun { get; set; }

  [CliOption(Description = "Only these tools, comma-separated", Required = false)]
  public string? Tools { get; set; }

  [CliOption(Description = "Print JSON (implies a check)", Required = false)]
  public bool Json { get; set; }

  public Task<int> RunAsync()
  {
    var selected = Select();
    if (selected is null)
      return Task.FromResult(1);

    var manager = ToolRequirements.DetectPackageManager();
    var statuses = selected
      .Select(tool => (Tool: tool, Version: tool.Detect()))
      .ToList();

    var table = new Table().Title("[bold]Tools Forge uses[/]");
    table.AddColumn("Tool");
    table.AddColumn("Status");
    table.AddColumn("Version");
    table.AddColumn("For");

    var missing = new List<ToolRequirement>();
    var missingRequired = new List<ToolRequirement>();

    foreach (var (tool, version) in statuses)
    {
      var present = version is not null;
      var acceptable = tool.IsVersionAcceptable(version);

      if (!present || !acceptable)
      {
        missing.Add(tool);
        if (tool.Required)
          missingRequired.Add(tool);
      }

      var status = (present, acceptable) switch
      {
        (false, _) => tool.Required ? "[red]missing[/]" : "[yellow]missing (optional)[/]",
        (true, false) => $"[red]too old (needs {tool.MinimumVersion})[/]",
        _ => "[green]ok[/]"
      };

      table.AddRow(
        tool.Name,
        status,
        version is null ? "[dim]—[/]" : version,
        $"[dim]{tool.Purpose}[/]");
    }

    if (Json)
    {
      var sb = new System.Text.StringBuilder("[");
      for (var i = 0; i < statuses.Count; i++)
      {
        var (tool, version) = statuses[i];
        if (i > 0)
          sb.Append(',');
        sb.Append('{');
        sb.Append($"\"tool\":{JsonOutput.Quote(tool.Name)},");
        sb.Append($"\"version\":{JsonOutput.Quote(version ?? string.Empty)},");
        sb.Append($"\"installed\":{JsonOutput.Bool(version is not null)},");
        sb.Append($"\"required\":{JsonOutput.Bool(tool.Required)},");
        sb.Append($"\"minimumVersion\":{JsonOutput.Quote(tool.MinimumVersion)},");
        sb.Append($"\"packageManager\":{JsonOutput.Quote(ToolRequirements.DetectPackageManager() ?? string.Empty)}");
        sb.Append('}');
      }
      sb.Append(']');
      Console.WriteLine(sb.ToString());
      return Task.FromResult(missingRequired.Count > 0 ? 1 : 0);
    }

    AnsiConsole.Write(table);

    var missingExtras = new List<string>();
    foreach (var (name, purpose, hint) in ToolRequirements.ExtraSteps)
    {
      var present = ToolLocator.Find(name) is not null;
      if (!present)
        missingExtras.Add(name);

      AnsiConsole.MarkupLine(present
        ? $"   [green]ok[/] {name} [dim]({purpose})[/]"
        : $"   [yellow]missing (optional)[/] {name} [dim]({purpose})[/] — " +
          $"{ToolRequirements.HintFor(name, hint, manager)}");
    }

    AnsiConsole.WriteLine();

    if (manager is null)
    {
      AnsiConsole.MarkupLine("[yellow]No known package manager found[/] — install what is missing by hand.");
      return Task.FromResult(missingRequired.Count > 0 ? 1 : 0);
    }

    AnsiConsole.MarkupLine($"[dim]Package manager:[/] {manager}");

    if (!Install)
    {
      if (missing.Count == 0 && missingExtras.Count == 0)
      {
        AnsiConsole.MarkupLine("[green]Nothing to do.[/]");
      }
      else
      {
        var tools = missing.Count == 1 ? "1 tool" : $"{missing.Count} tools";
        var extras = missingExtras.Count == 0
          ? string.Empty
          : missingExtras.Count == 1
            ? $", plus the optional {missingExtras[0]}"
            : $", plus the optional {string.Join(" and ", missingExtras)}";

        AnsiConsole.MarkupLine(missing.Count > 0
          ? $"[yellow]{tools} missing{extras}[/] — run `forge setup --install` " +
            "(or `--install --dry-run` to see the commands)."
          : $"[yellow]The optional {string.Join(" and ", missingExtras)} " +
            $"{(missingExtras.Count == 1 ? "is" : "are")} missing[/] — see the commands above.");
      }
      return Task.FromResult(missingRequired.Count > 0 ? 1 : 0);
    }

    // A dry run shows the command for every selected tool (presence varies per
    // machine, the plan does not); a real run installs only what is missing.
    var planned = DryRun ? selected : missing;

    if (planned.Count == 0)
    {
      AnsiConsole.MarkupLine("[green]Nothing to install.[/]");
      return Task.FromResult(0);
    }

    if (DryRun && missing.Count > 0)
      AnsiConsole.MarkupLine($"[dim]Missing here: {string.Join(", ", missing.Select(t => t.Name))}[/]");

    var packages = planned.Select(tool => tool.PackageFor(manager)).Distinct().ToList();
    var (fileName, arguments, needsSudo) = ToolRequirements.InstallCommand(manager, packages);
    var commandLine = (needsSudo ? "sudo " : "") + fileName + " " + string.Join(" ", arguments);

    AnsiConsole.MarkupLine("[bold]Command:[/] " + commandLine);

    if (DryRun)
    {
      AnsiConsole.MarkupLine("[dim]--dry-run: nothing was run.[/]");
      ReportExtras(missingExtras, manager);
      return Task.FromResult(0);
    }

    if (!Yes)
    {
      if (Console.IsInputRedirected)
      {
        AnsiConsole.MarkupLine(
          "[yellow]Not a terminal[/] — re-run with `--yes` to install non-interactively.");
        return Task.FromResult(1);
      }

      if (!AnsiConsole.Confirm("Run it?", defaultValue: false))
      {
        AnsiConsole.MarkupLine("[dim]Nothing was installed.[/]");
        return Task.FromResult(1);
      }
    }

    try
    {
      var startInfo = new ProcessStartInfo(needsSudo ? "sudo" : fileName)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
      };

      if (needsSudo)
        startInfo.ArgumentList.Add(fileName);
      foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

      using var process = Process.Start(startInfo);
      if (process == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] could not start the package manager.");
        return Task.FromResult(1);
      }

      process.WaitForExit();
      if (process.ExitCode != 0)
      {
        AnsiConsole.MarkupLine($"[bold red]The package manager failed[/] (exit {process.ExitCode}).");
        return Task.FromResult(process.ExitCode);
      }
    }
    catch (Exception exception)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] {exception.Message}");
      return Task.FromResult(1);
    }

    ReportExtras(missingExtras, manager);

    // Report what changed, using the same detection as the check.
    AnsiConsole.WriteLine();
    var stillMissing = planned.Where(tool => tool.Detect() is null).ToList();
    if (stillMissing.Count == 0)
    {
      AnsiConsole.MarkupLine($"[green]Installed {planned.Count} tool(s).[/]");
      return Task.FromResult(0);
    }

    AnsiConsole.MarkupLine(
      $"[yellow]Still missing:[/] {string.Join(", ", stillMissing.Select(tool => tool.Name))}");
    return Task.FromResult(1);
  }

  /// <summary>
  /// Reminds the user about the ecosystem tools that are not packages of the
  /// table, with the command that installs each one on this machine.
  /// </summary>
  private static void ReportExtras(IReadOnlyList<string> missing, string? manager)
  {
    if (missing.Count == 0)
      return;

    AnsiConsole.MarkupLine(
      "[dim]Not installed by this command (they are ecosystems, not single packages):[/]");
    foreach (var (name, _, hint) in ToolRequirements.ExtraSteps)
    {
      if (missing.Contains(name))
        AnsiConsole.MarkupLine($"   [dim]{name}: {ToolRequirements.HintFor(name, hint, manager)}[/]");
    }
  }

  /// <summary>The tools to report on, or null when a name was unknown.</summary>
  private List<ToolRequirement>? Select()
  {
    if (string.IsNullOrWhiteSpace(Tools))
      return ToolRequirements.All.ToList();

    var selected = new List<ToolRequirement>();
    foreach (var name in Tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      var tool = ToolRequirements.Find(name);
      if (tool is null)
      {
        AnsiConsole.MarkupLine(
          $"[bold red]Error:[/] unknown tool `{name}`. Known: " +
          $"{string.Join(", ", ToolRequirements.All.Select(t => t.Name))}.");
        return null;
      }
      selected.Add(tool);
    }

    return selected;
  }
}
