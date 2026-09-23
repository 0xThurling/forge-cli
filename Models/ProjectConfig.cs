namespace forge.Models
{
  /// <summary>
  /// Represents the complete configuration of a Forge project as loaded from forge.lua.
  /// </summary>
  /// <remarks>
  /// This is the main container model that holds all configuration sections parsed from
  /// the forge.lua file. It includes project metadata, dependencies (both Git-based
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
    /// Extra build targets beside the project's own one (<c>targets</c> in
    /// <c>forge.lua</c>): a tools binary, a second library, a plugin.
    /// </summary>
    public List<ProjectTarget> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of Git-based dependencies managed via CMake FetchContent.
    /// </summary>
    /// <value>
    /// A dictionary mapping dependency names to their <see cref="Dependency"/> configuration.
    /// </value>
    public Dictionary<string, Dependency> Dependencies { get; set; } = [];

    /// <summary>
    /// pkg-config module names, resolved with pkg_check_modules.
    /// </summary>
    public List<string> PkgConfigDependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets vcpkg dependencies (manifest mode), keyed by package name.
    /// </summary>
    public Dictionary<string, VcpkgDependency> VcpkgDependencies { get; set; } = [];

    /// <summary>
    /// vcpkg checkout to use: an explicit path, otherwise <c>$VCPKG_ROOT</c>,
    /// otherwise <c>external/vcpkg</c>.
    /// </summary>
    public string VcpkgRoot { get; set; } = string.Empty;

    /// <summary>Optional <c>builtin-baseline</c> commit for vcpkg.json.</summary>
    public string VcpkgBaseline { get; set; } = string.Empty;

    /// <summary>Optional vcpkg triplet (e.g. <c>x64-linux</c>).</summary>
    public string VcpkgTriplet { get; set; } = string.Empty;

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

    public Dictionary<string, string> Custom { get; set; } = [];

    /// <summary>
    /// Gets or sets the compiler/linker flag configuration (`build` section).
    /// </summary>
    public BuildConfig Build { get; set; } = new();

    public bool Testing { get; set; } = false;

    /// <summary>Test framework used by the generated test target.</summary>
    public string TestFramework { get; set; } = "gtest";

    /// <summary>Build a Google Benchmark target from <c>bench/</c>.</summary>
    public bool Benchmark { get; set; }
  }

  public class FeatureConfig
  {
    public bool Enabled { get; set; } = false;
    public Dictionary<string, string> Options { get; set; } = [];
  }
}
