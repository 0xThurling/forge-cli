namespace forge.Models;

/// <summary>
/// An extra build target of a project, declared in the <c>targets</c> table.
/// </summary>
/// <remarks>
/// The project's own target (<c>project.name</c>) is unaffected; this adds
/// targets beside it — a tools binary, a second library, a plugin. Each one has
/// its own source directory and links the project's dependencies.
/// </remarks>
/// <example>
/// <code>
/// targets = {
///   { name = "tools", type = "executable", sources = { "tools" } },
///   { name = "shared", type = "library", sources = { "lib/shared" }, linkage = "shared" }
/// }
/// </code>
/// </example>
public class ProjectTarget
{
  /// <summary>The CMake target name.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary><c>executable</c> (default) or <c>library</c>.</summary>
  public string Type { get; set; } = "executable";

  /// <summary><c>static</c> (default) or <c>shared</c>, for libraries.</summary>
  public string Linkage { get; set; } = "static";

  /// <summary>
  /// Directories (or individual files) whose <c>.cpp</c> files belong to this
  /// target. Defaults to a directory named after the target.
  /// </summary>
  public List<string> Sources { get; set; } = [];

  /// <summary>
  /// Whether to emit install rules for this target. Defaults to true for
  /// libraries and false for executables.
  /// </summary>
  public bool? Install { get; set; }

  /// <summary>Whether this target gets install rules.</summary>
  public bool Installs => Install ?? Type == "library";
}
