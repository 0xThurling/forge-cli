using System.Diagnostics;
using forge.Models;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Builds and runs the project's executable.
  /// </summary>
  /// <remarks>
  /// This command is invoked when running `forge run` without arguments.
  /// It first builds the project using BuildCommand, then locates and executes
  /// the built executable.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Build and run the project
  /// forge run
  /// </code>
  /// </example>
  public class StartCommand
  {
    /// <summary>Which executable target to run (default: the project's own).</summary>
    public string? Bin { get; set; }

    /// <summary>Arguments passed to the program, in order.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = [];

    /// <summary>Skip the build and run the existing binary.</summary>
    public bool NoBuild { get; set; }

    public bool Release { get; set; }

    public bool Debug { get; set; }

    public int? Jobs { get; set; }

    public string? Standard { get; set; }

    public string? Preset { get; set; }

    public bool NoConfigPresets { get; set; }

    public bool Verbose { get; set; }

    /// <summary>
    /// Builds the project and executes the resulting binary.
    /// </summary>
    /// <returns>0 on success, non-zero on failure.</returns>
    public async Task<int> RunAsync()
    {
      if (!NoBuild)
      {
        var buildCommand = new BuildCommand
        {
          Release = Release,
          Debug = Debug,
          Jobs = Jobs,
          Standard = Standard,
          Preset = Preset,
          NoConfigPresets = NoConfigPresets,
          Verbose = Verbose,
        };

        if (await buildCommand.RunAsync() != 0)
        {
          return 1;
        }
      }

      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Could not load project config.");
        return 1;
      }

      if (config.Project.Type == "library")
      {
        return HandleLibraryBuild(config);
      }

      var wanted = string.IsNullOrWhiteSpace(Bin) ? config.Project.Name : Bin!;
      if (string.IsNullOrWhiteSpace(Bin) && config.Project.Type == "library")
      {
        // A library project's runnable target is an extra executable, if any.
        wanted = config.Targets.FirstOrDefault(target => target.Type == "executable")?.Name ?? wanted;
      }

      var executablePath = FindExecutable(wanted);

      if (string.IsNullOrEmpty(executablePath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] executable `{wanted}` not found.");
        if (config.Targets.Count > 0)
        {
          AnsiConsole.MarkupLine(
            $"[dim]Targets:[/] {config.Project.Name} ({config.Project.Type}), " +
            string.Join(", ", config.Targets.Select(target => $"{target.Name} ({target.Type})")));
        }
        AnsiConsole.MarkupLine("[dim]Searched in:[/]");
        AnsiConsole.MarkupLine($"   [dim]- build/{wanted}[/]");
        AnsiConsole.MarkupLine($"   [dim]- build/Release/{wanted}[/]");
        AnsiConsole.MarkupLine($"   [dim]- build/Debug/{wanted}[/]");
        return 1;
      }

      try
      {
        AnsiConsole.MarkupLine($"[cyan]Running:[/] {executablePath}");
        var processStartInfo = new ProcessStartInfo(executablePath)
        {
          UseShellExecute = false,
          RedirectStandardOutput = false,
          RedirectStandardError = false,
          CreateNoWindow = true,
        };
        foreach (var argument in Arguments)
          processStartInfo.ArgumentList.Add(argument);

        using var process = Process.Start(processStartInfo) ??
          throw new Exception("Failed to start process.");
        process.WaitForExit();
        return process.ExitCode;
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        return 1;
      }
    }

    private static string? FindExecutable(string name)
    {
      var possiblePaths = new[]{
        "build/" + name,
        "build/Release" + name,
        "build/Debug" + name,
        "build/" + name + ".exe",
        "build/Release" + name + ".exe",
      };

      foreach (var path in possiblePaths)
      {
        if (File.Exists(path))
        {
          return path;
        }
      }

      return null;
    }

    private static int HandleLibraryBuild(ProjectConfig config)
    {
      var possiblePaths = new[]
      {
        "build/lib" + config.Project.Name + ".a",
        "build/" + config.Project.Name + ".lib",
        "build/lib" + config.Project.Name + ".so",
        "build/" + config.Project.Name + ".dll"
      };

      string? foundPath = null;
      foreach (var path in possiblePaths)
      {
        if (File.Exists(path))
        {
          foundPath = path;
          break;
        }
      }

      if (foundPath != null)
      {
        var fileInfo = new FileInfo(foundPath);
        AnsiConsole.MarkupLine($"[green]Library built successfully![/]");
        AnsiConsole.MarkupLine($"   Path: {foundPath}");
        AnsiConsole.MarkupLine($"   Size: {fileInfo.Length / 1024.0:F2} KB");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Note:[/] Libraries cannot be executed directly.");
        AnsiConsole.MarkupLine($"[dim]To use this library, add it as a dependency in another project or include headers from src/[/]");
      }
      else
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Library output not found in build/ directory.");
        AnsiConsole.MarkupLine($"[dim]Expected: {string.Join(", ", possiblePaths)}[/]");
      }

      return 0;
    }
  }
}
