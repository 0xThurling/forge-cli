namespace forge.Models;

/// <summary>
/// A CMake section contributed by a Lua build script through
/// <c>forge.add_section(name, position, content)</c>.
/// </summary>
/// <remarks>
/// The position is resolved against the built-in sections, so a script can say
/// "after:project_target" instead of guessing at numbers. See
/// <see cref="forge.CMakeGeneration.CMakeRegistry"/> for the anchors.
/// </remarks>
public class LuaCmakeSection
{
  /// <summary>Identifier used in the generated file and in anchors.</summary>
  public required string Name { get; init; }

  /// <summary>
  /// Where the section goes: <c>"before:&lt;section&gt;"</c>,
  /// <c>"after:&lt;section&gt;"</c>, <c>"first"</c>, <c>"last"</c> (the
  /// default), or a raw priority number.
  /// </summary>
  public string Position { get; init; } = "last";

  /// <summary>The CMake to emit. <c>${PROJECT_NAME}</c> is substituted.</summary>
  public required string Content { get; init; }
}
