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

  /// <summary>
  /// Marks the ecosystems this project actually declares, so the output can say
  /// which of the offered tools a build would use.
  /// </summary>
  private void NoteExtrasTheProjectUses()
  {
    var config = ProjectConfigManager.LoadConfigAsync().GetAwaiter().GetResult();
    if (config == null)
      return;

    if (config.ConanDependencies.Count > 0)
      _extrasUsedByProject.Add("conan");
    if (config.VcpkgDependencies.Count > 0)
      _extrasUsedByProject.Add("vcpkg");
  }

  /// <summary>Ecosystem tools selected with <c>--tools</c> (conan, vcpkg).</summary>
  private readonly List<(string Name, string Purpose, string Hint)> _selectedExtras = [];

  /// <summary>Which of them this project's <c>forge.lua</c> actually declares.</summary>
  private readonly HashSet<string> _extrasUsedByProject = new(StringComparer.OrdinalIgnoreCase);

  public Task<int> RunAsync()
  {
    // Installing as root puts the results in root's home — pipx venvs, and
    // anything else that resolves `~`. Cheap to warn about, painful to debug.
    if (Install &&
        Environment.UserName == "root" &&
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUDO_USER")))
    {
      AnsiConsole.MarkupLine(
        "[yellow]Warning:[/] running as root — installed tools will land in root's home.");
    }

    var selected = Select();
    if (selected is null)
      return Task.FromResult(1);

    // `--install` without `--tools` covers every ecosystem, not only the ones
    // this project declares: the point of a setup command is that a fresh
    // machine is ready, whatever the next project needs. Each one is confirmed
    // separately (vcpkg is a checkout), so an irrelevant one costs a keystroke.
    if (Install && string.IsNullOrWhiteSpace(Tools))
    {
      foreach (var (name, purpose, hint) in ToolRequirements.ExtraSteps)
        _selectedExtras.Add((name, purpose, hint));

      NoteExtrasTheProjectUses();
    }

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
      // Found the way the *build* finds it, which is not always on PATH.
      var location = ToolRequirements.ResolveExtra(name);
      var broken = ToolRequirements.NeedsRepair().Any(entry => entry.Name == name);
      if (location is null && !broken)
        missingExtras.Add(name);

      AnsiConsole.MarkupLine(location switch
      {
        null when broken =>
          $"   [red]broken[/] {name} [dim]({purpose})[/] — see the broken links below",
        null => $"   [yellow]missing (optional)[/] {name} [dim]({purpose})[/] — " +
                $"{ToolRequirements.HintFor(name, hint, manager)}",
        _ when location == name => $"   [green]ok[/] {name} [dim]({purpose})[/]",
        // Conan has to be on PATH to be usable; vcpkg is used by path, so its
        // location is just information.
        _ when name == "conan" =>
          $"   [green]ok[/] {name} [dim]({purpose}; at {location} — add it to PATH with `pipx ensurepath`)[/]",
        _ => $"   [green]ok[/] {name} [dim]({purpose}; at {location})[/]"
      });
    }

    AnsiConsole.WriteLine();

    if (manager is null)
    {
      AnsiConsole.MarkupLine("[yellow]No known package manager found[/] — install what is missing by hand.");
      return Task.FromResult(missingRequired.Count > 0 ? 1 : 0);
    }

    AnsiConsole.MarkupLine($"[dim]Package manager:[/] {manager}");

    // Paths: a tool that is installed where the shell cannot see it is not
    // usable, which is the most confusing way for a setup to "succeed".
    var brokenLinks = ToolRequirements.NeedsRepair();
    if (brokenLinks.Count > 0)
    {
      AnsiConsole.MarkupLine("[bold]Broken links[/]");
      foreach (var (name, reason, fix) in brokenLinks)
      {
        AnsiConsole.MarkupLine($"   [red]{name}[/] {reason}");
        AnsiConsole.MarkupLine($"      [dim]fix:[/] {fix}");
      }

      if (Install && !DryRun)
      {
        foreach (var (name, _, fix) in brokenLinks)
        {
          if (!Confirm($"Run `{fix}`?"))
            continue;

          var parts = fix.Split(' ');
          var ran = RunProcess(parts[0], parts[1..]) == 0;
          if (ran && ToolRequirements.ResolveExtra(name) is not null)
          {
            AnsiConsole.MarkupLine($"   [green]repaired[/] {name}");
            continue;
          }

          // pipx refuses to replace a venv it did not create (an earlier
          // install as another user), and `--force` alone cannot fix that: the
          // venv has to go first.
          var venv = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "pipx", "venvs", name);

          if (Directory.Exists(venv) &&
              Confirm($"Remove the stale venv at {venv} and install {name} again?"))
          {
            try
            {
              Directory.Delete(venv, recursive: true);
            }
            catch (Exception exception)
            {
              AnsiConsole.MarkupLine($"[bold red]Could not remove it:[/] {exception.Message}");
              continue;
            }

            if (RunProcess("pipx", ["install", name]) == 0 &&
                ToolRequirements.ResolveExtra(name) is not null)
            {
              AnsiConsole.MarkupLine($"   [green]repaired[/] {name}");
            }
            else
            {
              AnsiConsole.MarkupLine($"[bold red]{name} is still not usable.[/]");
            }
          }
        }
      }

      AnsiConsole.WriteLine();
    }

    // Computed after any repair: a tool that was just relinked is visible now,
    // and the same run can also fix its PATH entry.
    var pathProblems = ToolRequirements.PathProblems();
    if (pathProblems.Count > 0)
    {
      AnsiConsole.MarkupLine("[bold]PATH[/]");
      foreach (var (name, location, fix) in pathProblems)
        AnsiConsole.MarkupLine($"   [yellow]{name}[/] is at {location}, which is not on PATH");

      // `--install` writes the fix: an export in the shell profile, or pipx's
      // own ensurepath when pipx is what put the tool there.
      var profile = ShellProfile.Path();
      var writes = new List<(string Line, string Why)>();

      var localBin = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
      var pathHasLocalBin = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(entry => Path.GetFullPath(entry).TrimEnd('/') == localBin.TrimEnd('/'));

      if (pathProblems.Any(problem => problem.Name == "conan") && !pathHasLocalBin)
      {
        var pathLine = OperatingSystem.IsWindows()
          ? "setx PATH \"%PATH%;" + localBin + "\""
          : "export PATH=\"" + localBin + ":$PATH\"";
        writes.Add((pathLine, "so pipx-installed tools are found"));
      }

      foreach (var (_, location, fix) in pathProblems.Where(problem => problem.Fix.StartsWith("export", StringComparison.Ordinal)))
        writes.Add((fix, "so vcpkg is found outside this project"));

      if (Install && !DryRun && writes.Count > 0)
      {
        AnsiConsole.MarkupLine($"[dim]shell profile:[/] {profile}");
        foreach (var (line, why) in writes)
        {
          if (!Confirm($"Add `{line}` ({why})?"))
            continue;

          if (ShellProfile.Ensure(line))
            AnsiConsole.MarkupLine($"   [green]added[/] {line}");
          else
            AnsiConsole.MarkupLine($"   [dim]already there:[/] {line}");
        }

        AnsiConsole.MarkupLine("[dim]Open a new shell (or `exec $SHELL`) to pick it up.[/]");
      }
      else if (writes.Count > 0)
      {
        AnsiConsole.MarkupLine($"[dim]shell profile ({profile}) would get:[/]");
        foreach (var (line, _) in writes)
          AnsiConsole.MarkupLine($"   [dim]{line}[/]");
      }

      AnsiConsole.WriteLine();
    }

    // The ecosystem tools are handled before the table's early return, so
    // `--tools conan` works on its own.
    if (Install)
    {
      foreach (var extra in _selectedExtras)
      {
        if (ToolRequirements.ResolveExtra(extra.Name) is not null)
        {
          AnsiConsole.MarkupLine($"[green]{extra.Name} is already installed.[/]");
          continue;
        }

        if (_extrasUsedByProject.Count > 0)
        {
          AnsiConsole.MarkupLine(_extrasUsedByProject.Contains(extra.Name)
            ? $"[dim]({extra.Name} is used by this project)[/]"
            : $"[dim]({extra.Name} is not used by this project — skip if you like)[/]");
        }

        if (extra.Name == "conan")
          InstallConan(manager, DryRun);
        else if (extra.Name == "vcpkg")
          InstallVcpkg(manager, DryRun);
      }
    }

    if (!Install)
    {
      if (missing.Count == 0 && missingExtras.Count == 0 &&
          pathProblems.Count == 0 && brokenLinks.Count == 0)
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

        if (missing.Count > 0)
        {
          AnsiConsole.MarkupLine(
            $"[yellow]{tools} missing{extras}[/] — run `forge setup --install` " +
            "(or `--install --dry-run` to see the commands).");
        }
        else if (missingExtras.Count > 0)
        {
          AnsiConsole.MarkupLine(
            $"[yellow]The optional {string.Join(" and ", missingExtras)} " +
            $"{(missingExtras.Count == 1 ? "is" : "are")} missing[/] — see the commands above.");
        }
        // Otherwise the PATH report above is the whole story.
      }
      return Task.FromResult(missingRequired.Count > 0 ? 1 : 0);
    }

    // A dry run shows the command for every selected tool (presence varies per
    // machine, the plan does not); a real run installs only what is missing.
    var planned = DryRun ? selected : missing;

    if (planned.Count == 0)
    {
      // Not when something else was handled or reported: the output would
      // claim nothing happened right after installing or flagging something.
      if (_selectedExtras.Count == 0 && pathProblems.Count == 0)
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
      ReportExtras(NotHandledExtras(missingExtras), manager);
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

    ReportExtras(NotHandledExtras(missingExtras), manager);

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
  /// Installs Conan with the machine's package manager, falling back to pipx
  /// where the archive package is still 1.x (Debian/Ubuntu).
  /// </summary>
  private bool InstallConan(string? manager, bool dryRun)
  {
    var steps = ToolRequirements.InstallStepsFor("conan", manager);
    if (steps.Count == 0)
    {
      AnsiConsole.MarkupLine(
        "[yellow]No known way to install conan here[/] — see https://conan.io.");
      return false;
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Conan[/]");
    foreach (var (fileName, arguments, needsSudo) in steps)
    {
      var commandLine = (needsSudo ? "sudo " : "") + fileName + " " + string.Join(" ", arguments);
      AnsiConsole.MarkupLine($"   [dim]{commandLine}[/]");
      if (dryRun)
        continue;

      if (!Confirm($"Run it?"))
        return false;

      if (RunProcess(needsSudo ? "sudo" : fileName,
            needsSudo ? [fileName, .. arguments] : arguments) != 0)
      {
        AnsiConsole.MarkupLine($"[bold red]`{commandLine}` failed.[/]");
        if (fileName != "pipx")
        {
          AnsiConsole.MarkupLine(
            "[dim]Not packaged here? `pipx install conan` works wherever Conan 2 is not.[/]");
        }
        return false;
      }
    }

    if (!dryRun)
    {
      // An exit code is not proof: pipx happily does nothing when it thinks the
      // package is installed. Verify, or say what is actually wrong.
      if (ToolRequirements.ResolveExtra("conan") is null)
      {
        AnsiConsole.MarkupLine(
          "[yellow]conan is still not usable.[/] If a stale venv is in the way, remove it and install again:");
        AnsiConsole.MarkupLine("   [dim]rm -rf ~/.local/share/pipx/venvs/conan && pipx install conan[/]");
        return false;
      }

      AnsiConsole.MarkupLine("[green]Conan installed.[/]");
    }

    return true;
  }

  /// <summary>
  /// Clones and bootstraps vcpkg into the project (<c>external/vcpkg</c>, which
  /// Forge looks for) or into the user's data directory, where it needs
  /// <c>VCPKG_ROOT</c>.
  /// </summary>
  private bool InstallVcpkg(string? manager, bool dryRun)
  {
    var inProject = File.Exists("forge.lua");
    var target = inProject
      ? Path.Combine("external", "vcpkg")
      : Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "forge", "vcpkg");

    var bootstrapped = OperatingSystem.IsWindows()
      ? Path.Combine(target, "vcpkg.exe")
      : Path.Combine(target, "vcpkg");

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]vcpkg[/]");

    if (File.Exists(bootstrapped))
    {
      AnsiConsole.MarkupLine($"   [green]already present[/] at {target}");
      return true;
    }

    // vcpkg's bootstrap needs these; without them it stops halfway through.
    var prerequisites = ToolRequirements.PackageNamesFor(
      "vcpkg", manager, ["curl", "zip", "unzip", "tar"]);

    var checkout = Directory.Exists(target);
    var bootstrap = OperatingSystem.IsWindows() ? "bootstrap-vcpkg.bat" : "bootstrap-vcpkg.sh";
    if (!checkout)
    {
      AnsiConsole.MarkupLine($"   [dim]git clone --depth 1 https://github.com/microsoft/vcpkg {target}[/]");
    }
    AnsiConsole.MarkupLine($"   [dim]cd {target} && {bootstrap} -disableMetrics[/]");

    if (prerequisites.Count > 0)
      AnsiConsole.MarkupLine($"   [dim]needs: {string.Join(", ", prerequisites)}[/]");

    if (dryRun)
      return true;

    if (prerequisites.Count > 0)
    {
      if (manager is null)
      {
        AnsiConsole.MarkupLine(
          "[yellow]Install these first:[/] " + string.Join(", ", prerequisites));
        return false;
      }

      var (file, arguments, needsSudo) = ToolRequirements.InstallCommand(manager, prerequisites);
      var commandLine = (needsSudo ? "sudo " : "") + file + " " + string.Join(" ", arguments);
      AnsiConsole.MarkupLine($"   [dim]{commandLine}[/]");

      if (!Confirm("Install vcpkg's prerequisites?"))
        return false;

      if (RunProcess(needsSudo ? "sudo" : file, needsSudo ? [file, .. arguments] : arguments) != 0)
      {
        AnsiConsole.MarkupLine($"[bold red]`{commandLine}` failed.[/]");
        return false;
      }
    }

    if (!Confirm(checkout
          ? $"Bootstrap the existing checkout at {target}?"
          : $"Clone and bootstrap vcpkg into {target}?"))
    {
      return false;
    }

    if (!checkout &&
        RunProcess("git", ["clone", "--depth", "1", "https://github.com/microsoft/vcpkg", target]) != 0)
    {
      AnsiConsole.MarkupLine("[bold red]Could not clone vcpkg.[/]");
      return false;
    }

    // Run it through the shell: a relative "./bootstrap-vcpkg.sh" is resolved
    // against the *current* directory, not the working directory.
    // `-disableMetrics`: a build tool should not turn on someone else's
    // telemetry in the user's checkout.
    var (interpreter, scriptArguments) = OperatingSystem.IsWindows()
      ? ("cmd", new List<string> { "/c", bootstrap, "-disableMetrics" })
      : ("bash", new List<string> { bootstrap, "-disableMetrics" });

    if (RunProcess(interpreter, scriptArguments, target) != 0)
    {
      // vcpkg's own bootstrap lists what it needs (curl, zip, unzip, tar);
      // translate that into the command for this machine.
      AnsiConsole.MarkupLine("[bold red]Could not bootstrap vcpkg.[/]");
      var stillMissing = ToolRequirements.PackageNamesFor("vcpkg", manager, ["curl", "zip", "unzip", "tar"]);
      if (stillMissing.Count > 0)
      {
        var (file, arguments, needsSudo) = ToolRequirements.InstallCommand(manager!, stillMissing);
        AnsiConsole.MarkupLine(
          "[dim]vcpkg's bootstrap needs curl, zip, unzip and tar — try:[/] " +
          (needsSudo ? "sudo " : "") + file + " " + string.Join(" ", arguments));
      }
      return false;
    }

    AnsiConsole.MarkupLine($"[green]vcpkg installed at {target}.[/]");
    if (!inProject)
    {
      AnsiConsole.MarkupLine(
        "[yellow]Set it up for Forge with:[/] export VCPKG_ROOT=" + Path.GetFullPath(target));
    }

    return true;
  }

  /// <summary>Asks before doing something heavy; `--yes` and non-interactive runs skip it.</summary>
  private bool Confirm(string question)
  {
    if (Yes)
      return true;

    if (Console.IsInputRedirected)
    {
      AnsiConsole.MarkupLine("[yellow]Not a terminal[/] — re-run with `--yes` to install non-interactively.");
      return false;
    }

    return AnsiConsole.Confirm(question, defaultValue: false);
  }

  /// <summary>Runs a command, optionally in a working directory.</summary>
  private static int RunProcess(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
  {
    try
    {
      var startInfo = new ProcessStartInfo(fileName)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
      };
      if (workingDirectory is not null)
        startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
      foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

      using var process = Process.Start(startInfo);
      if (process == null)
        return 1;
      process.WaitForExit();
      return process.ExitCode;
    }
    catch (Exception exception)
    {
      AnsiConsole.MarkupLine($"[bold red]Error:[/] {exception.Message}");
      return 1;
    }
  }

  /// <summary>The extras this run did not handle, so the closing note stays honest.</summary>
  private List<string> NotHandledExtras(List<string> missing) =>
    missing.Where(name => _selectedExtras.All(extra => extra.Name != name)).ToList();

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
        // The ecosystem tools are not packages of the table, but they can be
        // selected (and installed) the same way.
        var extra = ToolRequirements.ExtraSteps.FirstOrDefault(step =>
          string.Equals(step.Name, name, StringComparison.OrdinalIgnoreCase));
        if (extra.Name is not null)
        {
          _selectedExtras.Add(extra);
          continue;
        }

        AnsiConsole.MarkupLine(
          $"[bold red]Error:[/] unknown tool `{name}`. Known: " +
          $"{string.Join(", ", ToolRequirements.All.Select(t => t.Name))}, " +
          $"{string.Join(", ", ToolRequirements.ExtraSteps.Select(step => step.Name))}.");
        return null;
      }
      selected.Add(tool);
    }

    return selected;
  }
}
