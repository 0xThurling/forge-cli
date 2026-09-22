using DotMake.CommandLine;

namespace forge.Commands;

/// <summary>
/// Builds the workspace projects in dependency order.
/// </summary>
/// <remarks>
/// Each project is built by re-running <c>forge build</c> inside it, so the
/// output and the behaviour are exactly those of a normal build. Naming one or
/// more projects limits the run to them and their local dependencies.
/// </remarks>
[CliCommand(Name = "build", Description = "Build the workspace projects in dependency order.", Parent = typeof(WorkspaceCommand))]
public class WorkspaceBuildCommand
{
  [CliArgument(Description = "Projects to build (default: all).", Required = false)]
  public string[] Projects { get; set; } = [];

  [CliOption(Description = "Parallel build jobs per project (default: all cores)", Required = false)]
  public int? Jobs { get; set; }

  [CliOption(Description = "Show the build order without building", Required = false)]
  public bool DryRun { get; set; }

  public async Task<int> RunAsync()
  {
    var plan = await WorkspaceRunner.PlanAsync(Projects);
    if (plan == null)
      return 1;

    var arguments = new List<string>();
    if (Jobs.HasValue)
      arguments.AddRange(["--jobs", Jobs.Value.ToString()]);

    return await WorkspaceRunner.RunAsync(plan.Value.Projects, "build", arguments, DryRun);
  }
}
