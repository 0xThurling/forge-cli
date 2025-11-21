using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DotMake.CommandLine;
using Spectre.Console;

namespace cpm.Commands.Conan {
  [CliCommand(Name = "install", Description = "Generate conanfile and install dependencies", Parent = typeof(RootCommand))]
  public partial class InstallCommand {
    public int Run() {
      var config = ProjectConfigManager.LoadConfig();
      if (config == null) return 1;

      if (config.ConanDependencies == null || config.ConanDependencies.Count == 0)
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

      try
      {
        var processInfo = new ProcessStartInfo("conan", $"install {conanfilePath} --output-folder=build --build=missing") {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = Process.Start(processInfo) ?? throw new Exception("Failed to start conan process.");
         
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        LinkConanDependencies(errorBuilder.ToString());
        FindConanDependencies(errorBuilder.ToString());

        if (process.ExitCode != 0)
        {
          AnsiConsole.WriteLine(errorBuilder.ToString());
          return 1;
        }

        return process.ExitCode;
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        return 1;
      }
    }

    /// <summary>
    ///   Extracts the correct CMake tags for the target_link_libraries
    ///   Used during the build process
    /// </summary>
    private static void LinkConanDependencies(string output) {
      var lines = output.Split('\n');

      foreach(var line in lines) {
        var trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("target_link_libraries")) {
          var linkTags = trimmedLine.Split(' ')[1..];
          foreach (var tag in linkTags) {
            var strippedTag = tag.Replace(')', ' ').Trim();
            ProjectBuildManager.LinkDependencies.Add(strippedTag);
          }
        }
      }
    }

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex MyRegex();

    private static void FindConanDependencies(string output) {
      var lines = output.Split('\n');

      foreach(var line in lines) {
        var trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("find_package")) {
          Match match = MyRegex().Match(trimmedLine);
          if (match.Success)
          {
              string extracted = match.Groups[1].Value;
              ProjectBuildManager.FindDependencies.Add(extracted);
          }
        }
      }
    }
  } 
}
