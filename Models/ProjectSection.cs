namespace forge.Models
{
  /// <summary>
  /// Represents the project section of the forge.lua configuration file.
  /// </summary>
  /// <remarks>
  /// Contains core project metadata that defines the project's identity and build type.
  /// This section is required in every Forge project configuration.
  /// </remarks>
  public class ProjectSection
  {
    /// <summary>
    /// Gets or sets the unique identifier name for the project.
    /// </summary>
    /// <value>
    /// A non-empty string used as the project name and CMake project name.
    /// </value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of project to build.
    /// </summary>
    /// <value>
    /// "executable" for a runnable application, or "library" for a static library.
    /// Defaults to "executable".
    /// </value>
    public string Type { get; set; } = "executable"; // Default to executable

    // Sets the library linking method (static | shared)
    public string Linkage { get; set; } = "static";

    ///
    public string Standard { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to install headers when building a library.
    /// </summary>
    /// <value>
    /// If true, headers are installed to the install prefix. Only applicable when
    /// Type is set to "library". Defaults to false.
    /// </value>
    public bool InstallHeaders { get; set; }
  }
}
