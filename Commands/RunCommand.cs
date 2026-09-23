using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Runs the built executable or a custom script defined in package.toml.
  /// </summary>
  /// <remarks>
  /// When invoked without arguments, this command builds and runs the project's
  /// executable. When invoked with a script name, it runs the corresponding
  /// script defined in the [scripts] section of package.toml.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Build and run the project executable
  /// forge run
  /// 
  /// // Run a custom script
  /// forge run compile-shaders
  /// </code>
  /// </example>
  [CliCommand(Name = "run", Description = "Run a custom script.", Parent = typeof(RootCommand))]
  public class RunCommand
  {
    /// <summary>
    /// Gets or sets the name of the script to execute.
    /// </summary>
    /// <value>
    /// The script name as defined in package.toml [scripts] section. If empty,
    /// builds and runs the main executable.
    /// </value>
    [CliArgument(Description = "Name of the script to run.", Required = false)]
    public string? ScriptName { get; set; }

    /// <summary>
    /// Arguments for the script, or — with `forge run -- …` — for the program.
    /// </summary>
    [CliArgument(Description = "Arguments for the script or program.", Required = false)]
    public string[] Arguments { get; set; } = [];

    /// <summary>Which executable target to run (default: the project's own).</summary>
    [CliOption(Description = "Executable target to run (default: the project's)", Required = false)]
    public string? Bin { get; set; }

    /// <summary>Run the existing binary instead of building first.</summary>
    [CliOption(Description = "Skip the build and run the existing binary", Required = false)]
    public bool NoBuild { get; set; }

    /// <summary>Production preset (-O3 -DNDEBUG) regardless of forge.lua.</summary>
    [CliOption(Description = "Build with the production preset regardless of config.")]
    public bool Release { get; set; }

    /// <summary>Debug preset (-O0 -g) regardless of forge.lua.</summary>
    [CliOption(Description = "Build with the debug preset regardless of config.")]
    public bool Debug { get; set; }

    /// <summary>Parallel build jobs (default: all cores).</summary>
    [CliOption(Description = "Parallel build jobs (default: all cores)", Required = false)]
    public int? Jobs { get; set; }

    /// <summary>Overrides the project's C++ standard for this invocation.</summary>
    [CliOption(Description = "C++ standard to use (e.g., 11, 14, 17, 20). Defaults to the configured standard.", Required = false)]
    public string? Standard { get; set; }

    /// <summary>Extra presets for a quick build (comma-separated).</summary>
    [CliOption(Description = "Add build presets for a quick build (comma-separated).", Required = false)]
    public string? Preset { get; set; }

    /// <summary>Ignore presets declared in forge.lua.</summary>
    [CliOption(Description = "Ignore presets declared in forge.lua (use only CLI presets).")]
    public bool NoConfigPresets { get; set; }

    /// <summary>
    /// Executes the project executable or a named script.
    /// </summary>
    /// <returns>
    /// 0 if the execution completed successfully, non-zero if there was an error.
    /// </returns>
    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();

      var startCommand = new StartCommand
      {
        Bin = Bin,
        Arguments = Arguments,
        NoBuild = NoBuild,
        Release = Release,
        Debug = Debug,
        Jobs = Jobs,
        Standard = Standard,
        Preset = Preset,
        NoConfigPresets = NoConfigPresets,
      };

      // No name: build and run the project's executable.
      if (string.IsNullOrEmpty(ScriptName))
        return await startCommand.RunAsync();

      // A first positional that is not a script name is a program argument
      // (`forge run -- --flag`, and flags DotMake already recognised).
      var isScript = config!.Scripts.ContainsKey(ScriptName!) ||
                     ProjectCommands.CommandFor(ScriptName!) is not null;
      if (!isScript && ScriptName!.StartsWith('-'))
      {
        startCommand.Arguments = [ScriptName, .. Arguments];
        return await startCommand.RunAsync();
      }

      // The status callback's result carries the script's exit code: a failing
      // script must fail `forge run` too.
      var scriptName = ScriptName!;
      var scriptArguments = Arguments;
      return AnsiConsole.Status().Start($"Running {scriptName}", _ =>
      {
        // forge.lua scripts win; otherwise a script file in
        // .config/forge/commands/ of the same name is used.
        if (!config.Scripts.TryGetValue(scriptName, out var scriptCommand))
        {
          scriptCommand = ProjectCommands.CommandFor(scriptName);
        }

        if (scriptCommand is null)
        {
          AnsiConsole.MarkupLine(
            $"[bold red]Error:[/] Script '[bold]{scriptName}[/]' not found in forge.lua or .config/forge/commands/.");
          return 1;
        }

        try
        {
          // The script is passed as a single argument to bash, with extra
          // arguments as positional parameters ($1, $2, …) — no re-quoting.
          var processStartInfo = new ProcessStartInfo("bash")
          {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
          };
          processStartInfo.ArgumentList.Add("-c");
          processStartInfo.ArgumentList.Add(scriptCommand);
          processStartInfo.ArgumentList.Add("bash");
          foreach (var argument in scriptArguments)
            processStartInfo.ArgumentList.Add(argument);

          using var process = Process.Start(processStartInfo) ?? throw new Exception("Failed to start script process.");
          process.WaitForExit();
          return process.ExitCode;
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
          return 1;
        }
      });
    }
  }
}
