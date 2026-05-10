using DotMake.CommandLine;

namespace forge.Commands
{
  /// <summary>
  /// Parent command for project information subcommands.
  /// </summary>
  /// <remarks>
  /// This command serves as the parent for subcommands that provide project
  /// information including configuration, file tree, statistics, dependencies, and scripts.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Display project information
  /// forge project info
  /// 
  /// // Show project file tree
  /// forge project tree
  /// 
  /// // List dependencies
  /// forge project dependencies
  /// </code>
  /// </example>
  [CliCommand(Name = "project", Description = "Commands for managing the project.", Parent = typeof(RootCommand))]
  public class ProjectCommand {}
}
