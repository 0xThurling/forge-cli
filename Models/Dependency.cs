namespace forge.Models
{
  /// <summary>
  /// Represents a Git-based dependency managed via CMake FetchContent.
  /// </summary>
  /// <remarks>
  /// This model defines a dependency that will be downloaded from a Git repository
  /// during the CMake configuration phase using FetchContent_Declare and FetchContent_MakeAvailable.
  /// </remarks>
  public class Dependency
  {
    /// <summary>
    /// Gets or sets the Git repository URL from which to fetch the dependency.
    /// </summary>
    /// <value>
    /// A valid HTTP/HTTPS URL to a Git repository.
    /// </value>
    public string Git { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Git tag, branch, or commit SHA to checkout.
    /// </summary>
    /// <value>
    /// A Git tag (e.g., "v1.0.0"), branch name (e.g., "main"), or commit SHA.
    /// </value>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a local directory to use as the dependency's source instead
    /// of fetching it from Git.
    /// </summary>
    /// <remarks>
    /// Use this when the dependency lives beside the project (a workspace of
    /// sibling checkouts) and is being edited at the same time — no commit,
    /// push or tag round-trip is needed. Relative paths are resolved against
    /// the project directory. When set, <see cref="Git"/> and <see cref="Tag"/>
    /// are ignored.
    /// </remarks>
    /// <value>
    /// A path to a directory containing the dependency's CMakeLists.txt.
    /// </value>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CMake target name to use when linking this dependency.
    /// </summary>
    /// <value>
    /// The CMake target identifier (e.g., "SDL2::SDL2"). If empty, defaults to
    /// the dependency key name from the configuration.
    /// </value>
    public string Target { get; set; } = string.Empty; // Optional, defaults to key name
  }
}
