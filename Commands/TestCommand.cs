using System.Diagnostics;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Builds and runs Google Test-based unit tests for the project.
  /// </summary>
  /// <remarks>
  /// This command sets up Google Test if not already configured, builds the project
  /// including tests, and executes the test suite. Tests are expected to be in the
  /// test/ directory and the project must have googletest as a dependency.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Run all tests
  /// forge test
  /// 
  /// // Run a specific test suite
  /// forge test MyTestSuite
  /// 
  /// // Run tests with filter
  /// forge test --filter="MathTest.*"
  /// </code>
  /// </example>
  [CliCommand(Name = "test", Description = "Build and run tests.", Parent = typeof(RootCommand))]
  public class TestCommand
  {
    /// <summary>
    /// Gets or sets the name of the test suite to run.
    /// </summary>
    /// <value>
    /// The name of a Google Test suite. When provided, runs only tests in that suite.
    /// </value>
    [CliArgument(Description = "Optional: Name of the test suite to run (e.g., MyTestSuite).", Required = false)]
    public string? TestSuiteName { get; set; }

    /// <summary>
    /// Gets or sets a Google Test filter to select which tests to run.
    /// </summary>
    /// <value>
    /// A Google Test filter string (e.g., "TestSuite.TestName" or "TestSuite.*").
    /// </value>
    [CliOption(Description = "Filter tests to run (e.g., MyTestSuite.TestName or MyTestSuite.*).", Required = false)]
    public string? Filter { get; set; }

    /// <summary>
    /// Gets or sets the C++ standard version to use for building tests.
    /// </summary>
    /// <value>
    /// Valid values: "11", "14", "17", "20". Defaults to "20".
    /// </value>
    /// <summary>Parallel build jobs, forwarded to the build step.</summary>
    [CliOption(Description = "Parallel build jobs (default: all cores)", Required = false)]
    public int? Jobs { get; set; }

    /// <summary>
    /// Overrides the project's C++ standard for this invocation. Null when the
    /// flag was not given, so the configured standard wins.
    /// </summary>
    [CliOption(Description = "C++ standard to use (e.g., 11, 14, 17, 20). Defaults to the configured standard.", Required = false)]
    public string? Standard { get; set; }

    /// <summary>Production preset (-O3 -DNDEBUG) regardless of forge.lua.</summary>
    [CliOption(Description = "Build tests with the production preset regardless of config.")]
    public bool Release { get; set; }

    /// <summary>Debug preset (-O0 -g) regardless of forge.lua.</summary>
    [CliOption(Description = "Build tests with the debug preset regardless of config.")]
    public bool Debug { get; set; }

    /// <summary>Extra presets for a quick build (comma-separated).</summary>
    [CliOption(Description = "Add build presets for a quick build (comma-separated).", Required = false)]
    public string? Preset { get; set; }

    /// <summary>Ignore presets declared in forge.lua.</summary>
    [CliOption(Description = "Ignore presets declared in forge.lua (use only CLI presets).")]
    public bool NoConfigPresets { get; set; }

    /// <summary>
    /// Writes a JUnit XML report (ctest --output-junit), which CI test reporters
    /// and IDEs understand.
    /// </summary>
    [CliOption(Description = "Write a JUnit XML report to this path", Required = false)]
    public string? JUnit { get; set; }

    /// <summary>Production preset (-O3 -DNDEBUG) regardless of forge.lua.</summary>
    [CliOption(Description = "Build tests with the production preset regardless of config.")]
    public bool Release { get; set; }

    /// <summary>Debug preset (-O0 -g) regardless of forge.lua.</summary>
    [CliOption(Description = "Build tests with the debug preset regardless of config.")]
    public bool Debug { get; set; }

    /// <summary>Extra presets for a quick build (comma-separated).</summary>
    [CliOption(Description = "Add build presets for a quick build (comma-separated).", Required = false)]
    public string? Preset { get; set; }

    /// <summary>Ignore presets declared in forge.lua.</summary>
    [CliOption(Description = "Ignore presets declared in forge.lua (use only CLI presets).")]
    public bool NoConfigPresets { get; set; }

    /// <summary>
    /// Executes the test build and run pipeline.
    /// </summary>
    /// <returns>
    /// 0 if all tests pass, non-zero if tests fail or build fails.
    /// </returns>
    public async Task<int> RunAsync()
    {
      if (!Directory.Exists("test"))
      {
        await Utils.CreateTests();
      }

      // Build the project (which includes tests if googletest is present)
      var buildCommand = new BuildCommand
      {
        Verbose = false, // Tests usually don't need verbose build output
        Jobs = Jobs,
        Standard = Standard,
        Release = Release,
        Debug = Debug,
        Preset = Preset,
        NoConfigPresets = NoConfigPresets,
      };

      if (await buildCommand.RunAsync() != 0)
      {
        return 1; // Build failed
      }

      AnsiConsole.Status().Start("Running Tests...", _ =>
      {
        // Prefer CTest: gtest_discover_tests registers the suite on configure,
        // and the test target is named `<project>_tests` (not `run_tests`).
        var ctestArgs = new List<string> { "--test-dir", "build", "--output-on-failure" };

        // Tests run in parallel, like the build: for a large suite the tests,
        // not the compile, are the slow part of `forge test`.
        ctestArgs.Add("-j");
        ctestArgs.Add((Jobs is > 0 ? Jobs.Value : Environment.ProcessorCount).ToString());

        if (!string.IsNullOrWhiteSpace(JUnit))
        {
          // ctest runs inside --test-dir, so a relative report path would land
          // in build/. Resolve it against the project directory instead.
          var reportPath = Path.GetFullPath(JUnit);
          var reportDirectory = Path.GetDirectoryName(reportPath);
          if (!string.IsNullOrEmpty(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
          ctestArgs.Add("--output-junit");
          ctestArgs.Add(reportPath);
        }
        var gtestFilter = Filter;
        if (string.IsNullOrEmpty(gtestFilter) && !string.IsNullOrEmpty(TestSuiteName))
        {
          gtestFilter = $"{TestSuiteName}.*";
        }
        if (!string.IsNullOrEmpty(gtestFilter))
        {
          ctestArgs.Add("-R");
          ctestArgs.Add(gtestFilter);
        }

        try
        {
          var ctest = RunProcess("ctest", ctestArgs);
          if (ctest.HasValue)
          {
            if (ctest.Value != 0)
            {
              AnsiConsole.MarkupLine("[bold red]Tests failed.[/]");
              return 1;
            }
            AnsiConsole.MarkupLine("[bold green]All tests passed.[/]");
            return 0;
          }

          // Fallback: run the test executable discovered in build/ (`*_tests`).
          var testExecutable = Directory.Exists("build")
            ? Directory.GetFiles("build", "*_tests").FirstOrDefault()
            : null;
          if (testExecutable == null)
          {
            AnsiConsole.MarkupLine("[bold red]Error:[/] Test executable not found. Ensure googletest is a dependency and project builds correctly.");
            return 1;
          }

          var directArgs = new List<string>();
          if (!string.IsNullOrEmpty(gtestFilter))
          {
            directArgs.Add($"--gtest_filter={gtestFilter}");
          }

          if (RunProcess(testExecutable, directArgs) is not 0)
          {
            AnsiConsole.MarkupLine("[bold red]Tests failed.[/]");
            return 1;
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

    /// <summary>
    /// Runs a process and returns its exit code, or null if the executable
    /// could not be started (e.g. not installed).
    /// </summary>
    private static int? RunProcess(string fileName, IEnumerable<string> args)
    {
      var startInfo = new ProcessStartInfo(fileName)
      {
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = true,
      };
      foreach (var arg in args)
        startInfo.ArgumentList.Add(arg);

      try
      {
        using var process = Process.Start(startInfo);
        if (process == null)
          return null;
        process.WaitForExit();
        return process.ExitCode;
      }
      catch (System.ComponentModel.Win32Exception)
      {
        return null; // executable not found
      }
    }
  }
}
