using DotMake.CommandLine;

namespace forge.Commands
{
  /// <summary>
  /// The root command for the Forge CLI application.
  /// </summary>
  /// <remarks>
  /// This is the top-level command that serves as the parent for all other commands
  /// in the Forge CLI. It provides no functionality itself but defines the command
  /// hierarchy using DotMake.CommandLine's parent-child relationship system.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Usage: forge [subcommand] [options]
  /// forge create myproject
  /// forge build
  /// forge run
  /// </code>
  /// </example>
  [CliCommand(Description = "A C++ project manager.")]
  public class RootCommand
  {
  }
}
