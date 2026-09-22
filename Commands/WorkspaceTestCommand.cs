using DotMake.CommandLine;

namespace forge.Commands;

/// <summary>
/// Builds and runs the tests of every workspace project, in dependency order.
/// </summary>
[CliCommand(Name = "test", Description = "Test the workspace projects in dependency order.", Parent = typeof(WorkspaceCommand))]
public class WorkspaceTestCommand
{
  [CliArgument(Description = "Projects to test (default: all).", Required = false)]
  public string[] Projects { get; set; } = [];

  [CliOption(Description = "Test name filter passed to each project", Required = false)]
  public string? Filter { get; set; }

  [CliOption(Description = "Show the test order without running", Required = false)]
  public bool DryRun { get; set; }

  public async Task<int> RunAsync()
  {
    var plan = await WorkspaceRunner.PlanAsync(Projects);
    if (plan == null)
      return 1;

    var arguments = new List<string>();
    if (!string.IsNullOrWhiteSpace(Filter))
      arguments.AddRange(["--filter", Filter]);

    return await WorkspaceRunner.RunAsync(plan.Value.Projects, "test", arguments, DryRun);
  }
}
