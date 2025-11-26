using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  [CliCommand(Name = "test", Description = "Build and run tests.", Parent = typeof(RootCommand))]
  public class TestCommand
  {
    [CliArgument(Description = "Optional: Name of the test suite to run (e.g., MyTestSuite).")]
    public string? TestSuiteName { get; set; }

    [CliOption(Description = "Filter tests to run (e.g., MyTestSuite.TestName or MyTestSuite.*).")]
    public string? Filter { get; set; }

    [CliOption(Description = "C++ standard to use (e.g., 11, 14, 17, 20). Defaults to 20.")]
    public string Standard { get; set; } = "20";

    public int Run()
    {
      if (!Directory.Exists("test"))
      {
        Utils.CreateTests();
      }

      // Build the project (which includes tests if googletest is present)
      var buildCommand = new BuildCommand
      {
        Verbose = false, // Tests usually don't need verbose build output
        Standard = Standard
      };

      if (buildCommand.Run() != 0)
      {
        return 1; // Build failed
      }

      AnsiConsole.Status().Start("Running Tests...", _ =>
      {
        var testExecutable = Path.Combine("build", "run_tests");
        if (!File.Exists(testExecutable))
        {
          AnsiConsole.MarkupLine("[bold red]Error:[/] Test executable not found. Ensure googletest is a dependency and project builds correctly.");
          return 1;
        }

        try
        {
          var testCommandArgs = new List<string>();
          var gtestFilter = Filter;
          if (string.IsNullOrEmpty(gtestFilter) && !string.IsNullOrEmpty(TestSuiteName))
          {
            gtestFilter = $"{TestSuiteName}.*";
          }

          if (!string.IsNullOrEmpty(gtestFilter))
          {
            testCommandArgs.Add($"--gtest_filter={gtestFilter}");
          }

          var processStartInfo = new ProcessStartInfo(testExecutable)
          {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
          };

          foreach (var arg in testCommandArgs)
          {
            processStartInfo.ArgumentList.Add(arg);
          }

          using (var process = Process.Start(processStartInfo))
          {
            if (process == null) throw new Exception("Failed to start test process.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
              AnsiConsole.MarkupLine("[bold red]Tests failed.[/]");
              return 1;
            }
          }
          AnsiConsole.MarkupLine("[bold green]All tests passed.[/]");
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
          return 1;
        }

        return 0;
      });

      return 0;
    }
  }
}
