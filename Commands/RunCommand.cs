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
    /// Executes the project executable or a named script.
    /// </summary>
    /// <returns>
    /// 0 if the execution completed successfully, non-zero if there was an error.
    /// </returns>
    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();

      if (string.IsNullOrEmpty(ScriptName))
      {
        var startCommand = new StartCommand();
        return await startCommand.RunAsync();
      }

      // The status callback's result carries the script's exit code: a failing
      // script must fail `forge run` too.
      return AnsiConsole.Status().Start(
        ScriptName != null ? $"Running {ScriptName}" : "Running project...", _ =>
      {
        // forge.lua scripts win; otherwise a script file in
        // .config/forge/commands/ of the same name is used.
        if (!config!.Scripts.TryGetValue(ScriptName!, out var scriptCommand))
        {
          scriptCommand = ProjectCommands.CommandFor(ScriptName!);
        }

        if (scriptCommand is null)
        {
          AnsiConsole.MarkupLine(
            $"[bold red]Error:[/] Script '[bold]{ScriptName}[/]' not found in forge.lua or .config/forge/commands/.");
          return 1;
        }

        try
        {

          var processStartInfo = new ProcessStartInfo("bash", $"-c \"{scriptCommand}\"")
          {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
          };

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
