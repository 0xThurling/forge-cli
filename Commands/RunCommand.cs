using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "run", Description = "Run a custom script.", Parent = typeof(RootCommand))]
  public class RunCommand
  {
    [CliArgument(Description = "Name of the script to run.")]
    public string? ScriptName { get; set; }

    public int Run()
    {
      var config = ProjectConfigManager.LoadConfig();

      if (string.IsNullOrEmpty(ScriptName))
      {
        var startCommand = new StartCommand();
        return startCommand.Run();
      }

      AnsiConsole.Status().Start(ScriptName != null ? $"Running {ScriptName}" : "Running project...", _ =>
      {
        if (!config!.Scripts.TryGetValue(ScriptName!, out var scriptCommand))
        {
          AnsiConsole.MarkupLine($"[bold red]Error:[/] Script '[bold]{ScriptName}[/]' not found in package.toml.");
          return 1;
        }

        try
        {

          var processStartInfo = new ProcessStartInfo("bash", $"-c \"{scriptCommand}\"")
          {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
          };

          using var process = Process.Start(processStartInfo) ?? throw new Exception("Failed to start script process.");
          process.StandardOutput.ReadToEnd();
          process.StandardError.ReadToEnd();
          process.WaitForExit();
          return process.ExitCode;
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
          return 1;
        }
      });

      return 0;
    }
  }
}
