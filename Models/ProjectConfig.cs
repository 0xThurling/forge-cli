namespace forge.Models
{
  /// <summary>
  /// Represents the complete configuration of a Forge project as loaded from package.toml.
  /// </summary>
  /// <remarks>
  /// This is the main container model that holds all configuration sections parsed from
  /// the package.toml file. It includes project metadata, dependencies (both Git-based
  /// and Conan), resource files, and custom scripts.
  /// </remarks>
  /// <example>
  /// <code>
  /// var config = new ProjectConfig
  /// {
  ///     Project = new ProjectSection { Name = "myapp", Type = "executable" },
  ///     Dependencies = new Dictionary<string, Dependency>
  ///     {
  ///         ["sdl"] = new Dependency { Git = "...", Tag = "2.30.0" }
  ///     }
  /// };
  /// </code>
  /// </example>
  public class ProjectConfig
  {
    /// <summary>
    /// Gets or sets the project metadata section containing name, type, and build options.
    /// </summary>
    public ProjectSection Project { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of Git-based dependencies managed via CMake FetchContent.
    /// </summary>
    /// <value>
    /// A dictionary mapping dependency names to their <see cref="Dependency"/> configuration.
    /// </value>
    public Dictionary<string, Dependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets the collection of Conan package dependencies.
    /// </summary>
    /// <value>
    /// A dictionary mapping package names to version strings.
    /// </value>
    public Dictionary<string, string> ConanDependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the resources section containing files to embed in the executable.
    /// </summary>
    public ResourcesSection Resources { get; set; } = new();

    /// <summary>
    /// Gets the collection of custom scripts defined in the project configuration.
    /// </summary>
    /// <value>
    /// A dictionary mapping script names to shell command strings.
    /// </value>
    public Dictionary<string, string> Scripts { get; set; } = [];

    public Dictionary<string, FeatureConfig> Features { get; set; } = [];
  }

  public class FeatureConfig
  {
    public bool Enabled { get; set; } = false;
    public Dictionary<string, string> Options { get; set; } = [];
  }
}
