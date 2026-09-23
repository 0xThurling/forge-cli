using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DotMake.CommandLine;
using forge.CMakeGeneration;
using Spectre.Console;

namespace forge.Commands.Conan
{
  /// <summary>
  /// Installs Conan package dependencies and configures CMake integration.
  /// </summary>
  /// <remarks>
  /// This command generates a conanfile.txt from the [conan-dependencies] section
  /// of forge.lua and runs `conan install` to fetch and configure the packages.
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

    /// <summary>Re-resolve every locked git dependency to its current commit.</summary>
    [CliOption(Description = "Refresh forge.lock (re-resolve git refs)", Required = false)]
    public bool Update { get; set; }

    /// <summary>
    /// Generates conanfile.txt and runs Conan to install dependencies.
    /// </summary>
    /// <returns>0 on success, 1 on failure.</returns>
    public async Task<int> RunAsync() => await InstallAsync(lockDependencies: true);

    /// <summary>
    /// Used by <c>forge build</c>: installs Conan packages and leaves
    /// <c>forge.lock</c> alone, so a build never needs the network for a
    /// dependency that is already declared.
    /// </summary>
    public async Task<int> InstallForBuildAsync(BuildContext context, string buildType = "Release") =>
      await InstallAsync(lockDependencies: false, context: context, buildType: buildType);

    private async Task<int> InstallAsync(
      bool lockDependencies, BuildContext? context = null, string buildType = "Release")
    {
      // A standalone `forge install` has no build to contribute to; its parsed
      // targets only matter to a build, which passes its own context.
      context ??= new BuildContext();

      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null) return 1;

      if (config.ConanDependencies.Count == 0 && !string.IsNullOrEmpty(Prefix)) return InstallLib();

      if (config.ConanDependencies.Count == 0)
      {
        // Nothing for Conan, but git dependencies still get locked.
        if (lockDependencies)
          await LockfileManager.ResolveAsync(config, Update);
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
        // The build type decides where Conan's cmake_layout puts the toolchain
        // (build/build/<type>/generators), which is where the configure step
        // looks for it — so it has to match the configuration being built.
        var processInfo = new ProcessStartInfo(
          "conan",
          $"install {conanfilePath} --output-folder=build --build=missing -s build_type={buildType}")
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

        LinkConanDependencies(errorBuilder.ToString(), context);
        FindConanDependencies(errorBuilder.ToString(), context);

        if (process.ExitCode != 0)
        {
          AnsiConsole.WriteLine(errorBuilder.ToString());
          return 1;
        }

        if (!string.IsNullOrEmpty(Prefix))
        {
          return InstallLib();
        }

        if (lockDependencies)
          await LockfileManager.ResolveAsync(config, Update);
        return process.ExitCode;
      }
      catch (Exception ex)
      {
        // The common case by far: `conan` is not on PATH. Say what to do
        // instead of only echoing the process-start error.
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        AnsiConsole.MarkupLine(
          "[yellow]Conan is required by dependencies.conan but could not be run.[/] " +
          "Install it (https://conan.io) or remove the packages from forge.lua.");
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
    private static void LinkConanDependencies(string output, BuildContext context)
    {
      foreach (var line in output.Split('\n'))
      {
        var trimmedLine = line.Trim();

        if (!trimmedLine.StartsWith("target_link_libraries"))
          continue;

        // Accept both shapes: `target_link_libraries(fmt::fmt spdlog::spdlog)`
        // and `target_link_libraries fmt::fmt spdlog::spdlog`. A CMake-style
        // leading target name is dropped when it is followed by a keyword
        // (`target_link_libraries(my_app PRIVATE fmt::fmt)`).
        var payload = trimmedLine["target_link_libraries".Length..]
          .Trim()
          .Trim('(', ')');

        var tags = payload
          .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
          .Select(tag => tag.Trim())
          .Where(tag => tag.Length != 0)
          .ToList();

        static bool IsKeyword(string tag) =>
          tag is "PRIVATE" or "PUBLIC" or "INTERFACE";

        if (tags.Count > 1 && IsKeyword(tags[1]))
          tags.RemoveAt(0); // the library being linked, not a dependency

        foreach (var tag in tags.Where(tag => !IsKeyword(tag)))
          context.LinkDependencies.Add(tag);
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
    private static void FindConanDependencies(string output, BuildContext context)
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
            context.FindDependencies.Add(extracted);
          }
        }
      }
    }
  }
}
