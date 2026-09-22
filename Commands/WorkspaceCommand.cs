using DotMake.CommandLine;

namespace forge.Commands
{
  /// <summary>
  /// Parent command for working with a workspace of sibling Forge projects.
  /// </summary>
  /// <remarks>
  /// A workspace is a directory holding several projects. With a
  /// <c>forge.workspace.lua</c> listing them (<c>projects = { "fp", "ml" }</c>)
  /// the list is explicit; without one, every subdirectory containing a
  /// <c>forge.lua</c> is a member.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge workspace list
  /// forge workspace build
  /// forge workspace test fp
  /// </code>
  /// </example>
  [CliCommand(Name = "workspace", Description = "Work with several sibling projects at once.", Parent = typeof(RootCommand))]
  public class WorkspaceCommand { }
}
