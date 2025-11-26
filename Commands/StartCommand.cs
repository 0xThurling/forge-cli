using System.Diagnostics;
using Spectre.Console;

namespace forge.Commands
{
    public class StartCommand
    {
        public int Run()
        {
            var buildCommand = new BuildCommand();
            if (buildCommand.Run() != 0)
            {
                return 1;
            }

            var projectName = ProjectConfigManager.GetProjectName();
            if (string.IsNullOrEmpty(projectName))
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Could not find project name to run.");
                return 1;
            }

            var executablePath = Path.Combine("build", projectName);
            if (!File.Exists(executablePath))
            {
                AnsiConsole.MarkupLine($"[bold red]Error:[/] Executable not found at '[bold]{executablePath}[/]'.");
                return 1;
            }

            try
            {
                var processStartInfo = new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null) throw new Exception("Failed to start program process.");
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                return 1;
            }
        }
    }
}
