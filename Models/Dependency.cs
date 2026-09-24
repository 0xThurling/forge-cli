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

    /// <summary>
    /// Gets or sets the CMake package to <c>find_dependency()</c> when this
    /// dependency is consumed from an installed package.
    /// </summary>
    /// <remarks>
    /// Set together with <see cref="ExportTarget"/>. The generated
    /// <c>&lt;project&gt;Config.cmake</c> then finds this package before loading
    /// the targets, so an installed library keeps its dependencies instead of
    /// dropping them. Ignored for dependencies the project does not install.
    /// </remarks>
    /// <value>
    /// A package name CMake can find, e.g. <c>"forgefp"</c>.
    /// </value>
    public string ExportPackage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the imported target <see cref="ExportPackage"/> provides,
    /// referenced by the installed interface.
    /// </summary>
    /// <value>
    /// A namespaced target, e.g. <c>"forgefp::forgefp"</c>.
    /// </value>
    public string ExportTarget { get; set; } = string.Empty;

    /// <summary>True when both export fields are set.</summary>
    public bool IsExportable =>
      !string.IsNullOrWhiteSpace(ExportPackage) && !string.IsNullOrWhiteSpace(ExportTarget);

    /// <summary>
    /// CMake cache variables set before the dependency is configured, e.g.
    /// <c>{ SDL_TEST = "OFF" }</c> to skip a dependency's own tests and examples.
    /// </summary>
    /// <remarks>
    /// Emitted as <c>set(&lt;key&gt; &lt;value&gt; CACHE STRING "" FORCE)</c> right
    /// before <c>FetchContent_MakeAvailable</c>, so the fetched project sees them
    /// as its own options.
    /// </remarks>
    public Dictionary<string, string> Options { get; set; } = [];
  }
}
