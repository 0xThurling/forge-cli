using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands.Conan
{
  /// <summary>
  /// Installs Conan package dependencies and configures CMake integration.
  /// </summary>
  /// <remarks>
  /// This command generates a conanfile.txt from the [conan-dependencies] section
  /// of package.toml and runs `conan install` to fetch and configure the packages.
  /// It also parses the Conan output to extract CMake target information for use
  /// during the build phase.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Install Conan dependencies
  /// forge install
  /// 
  /// // Or implicitly via build:
  /// forge build
  /// </code>
  /// </example>
  [CliCommand(Name = "install", Description = "Generate conanfile and install dependencies", Parent = typeof(RootCommand))]
  public partial class InstallCommand
  {
    [CliOption(Description = "The Directory to install the project to")]
    public string? Prefix { get; set; } = null;

    /// <summary>
    /// Generates conanfile.txt and runs Conan to install dependencies.
    /// </summary>
    /// <returns>0 on success, 1 on failure.</returns>
    public async Task<int> RunAsync()
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null) return 1;

      if (config.ConanDependencies.Count == 0 && !string.IsNullOrEmpty(Prefix)) return InstallLib();
      if (config.ConanDependencies.Count == 0) return 0;

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
        var processInfo = new ProcessStartInfo("conan", $"install {conanfilePath} --output-folder=build --build=missing")
        {
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

        if (!string.IsNullOrEmpty(Prefix))
        {
          return InstallLib();
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
    /// Extracts CMake target_link_libraries tags from Conan output.
    /// </summary>
    /// <param name="output">The Conan command output to parse.</param>
    /// <remarks>
    /// Parses lines starting with "target_link_libraries" and extracts the
    /// CMake target names for linking during the build phase.
    /// </remarks>
    private static void LinkConanDependencies(string output)
    {
      var lines = output.Split('\n');

      foreach (var line in lines)
      {
        var trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("target_link_libraries"))
        {
          var linkTags = trimmedLine.Split(' ')[1..];
          foreach (var tag in linkTags)
          {
            var strippedTag = tag.Replace(')', ' ').Trim();
            ProjectBuildManager.LinkDependencies.Add(strippedTag);
          }
        }
      }
    }

    private int InstallLib()
    {
      if (!string.IsNullOrEmpty(Prefix))
      {
        if (!Directory.Exists("build"))
        {
          AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] No build directory found. Did you run `forge build` first?");
          return 1;
        }

        AnsiConsole.MarkupLine($"[green]Installing Project to: {Prefix}[/]");

        var installationProcessInfo = new ProcessStartInfo("cmake", $"--install build --prefix {Prefix}")
        {
          UseShellExecute = false,
          RedirectStandardError = true,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };

        using var installationProcess = Process.Start(installationProcessInfo);
        installationProcess?.WaitForExit();

        if (installationProcess?.ExitCode != 0)
        {
          AnsiConsole.MarkupLine("[bold red]Installation failed.[/]");
          AnsiConsole.WriteLine(installationProcess?.StandardError.ReadToEnd() ?? "Unknown Error");
          return 1;
        }

        AnsiConsole.MarkupLine("[bold green]Installation Successful![/]");
      }

      return 0;
    }

    /// <summary>
    /// Generated regex pattern for extracting content from parentheses.
    /// </summary>
    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex MyRegex();

    /// <summary>
    /// Extracts find_package module names from Conan output.
    /// </summary>
    /// <param name="output">The Conan command output to parse.</param>
    /// <remarks>
    /// Parses lines starting with "find_package" and extracts the
    /// package names for CMake find_package() calls.
    /// </remarks>
    private static void FindConanDependencies(string output)
    {
      var lines = output.Split('\n');

      foreach (var line in lines)
      {
        var trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("find_package"))
        {
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
