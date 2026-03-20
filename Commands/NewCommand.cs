using DotMake.CommandLine;

namespace forge.Commands
{
  /// <summary>
  /// Parent command for code generation subcommands.
  /// </summary>
  /// <remarks>
  /// This command serves as the parent for subcommands that generate C++ boilerplate
  /// code including classes, structs, headers, and source files.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Generate a new class
  /// forge new class Player
  /// 
  /// // Generate a new struct
  /// forge new struct Vector3
  /// 
  /// // Generate a header and source pair
  /// forge new source Utils
  /// </code>
  /// </example>
  [CliCommand(Description = "Create a new entity.", Parent = typeof(RootCommand))]
  public class NewCommand
  {
  }
}
