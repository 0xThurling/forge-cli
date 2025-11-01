using System.Diagnostics;
using System.Text;
using DotMake.CommandLine;
using Spectre.Console;

namespace cpm.Commands {
  [CliCommand(Name = "install", Description = "Generate conanfile and install dependencies", Parent = typeof(RootCommand))]
  public class InstallCommand {
    public int Run() {
      var config = ProjectConfigManager.LoadConfig();
      if (config == null) return 1;

      if (config.ConanDependencies == null || !config.ConanDependencies.Any())
      {
        AnsiConsole.MarkupLine("[yellow]No conan dependencies defined in package.toml[/]");
        return 0;
      }

      Directory.CreateDirectory(".config");

      var conanfile = new StringBuilder();
      conanfile.AppendLine("[requires]");
      foreach (var dep in config.ConanDependencies)
      {
        conanfile.AppendLine($"{dep.Key}/{dep.Value}");
      }
      conanfile.AppendLine("\n[generators]\nCMakeDeps\nCMakeToolchain\n\n[layout]\ncmake_layout");
      
      var conanfilePath = Path.Combine(".config", "conanfile.txt");
      File.WriteAllText(conanfilePath, conanfile.ToString());
      AnsiConsole.MarkupLine($"[green]Generated {conanfilePath}[/]");

      AnsiConsole.MarkupLine("Running `conan install...`");

        var processInfo = new ProcessStartInfo("conan", $"install {conanfilePath} --output-folder=build --build=missing") {
          UseShellExecute = false,
          RedirectStandardOutput = false,
          RedirectStandardError = false 
        };
      try
      {

        using (var process = Process.Start(processInfo))
        {
           if (process == null) throw new Exception("Failed to start conan process.");
           process.WaitForExit();
           if (process.ExitCode != 0)
           {
             AnsiConsole.MarkupLine("[bold red] Conan install failed.[/]");
             AnsiConsole.WriteLine(process.StandardOutput.ReadToEnd());
             AnsiConsole.WriteLine(process.StandardError.ReadToEnd());
             return 1;
           }
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
