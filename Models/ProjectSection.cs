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

    /// <summary>Project version (used by vcpkg.json and the CMake export).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Derive the version from the newest Git tag instead of <see cref="Version"/>
    /// (<c>version_from_git = true</c>). Commits after the tag are appended as a
    /// fourth number, so the version stays numeric for `project(... VERSION ...)`.
    /// </summary>
    public bool VersionFromGit { get; set; }

    /// <summary>
    /// Runtime dependencies of the packages Forge builds, written into both the
    /// DEB and RPM metadata (<c>CPACK_DEBIAN_PACKAGE_DEPENDS</c>,
    /// <c>CPACK_RPM_PACKAGE_REQUIRES</c>).
    /// </summary>
    public List<string> PackageDepends { get; set; } = [];

    /// <summary>Runtime dependencies only the DEB metadata gets.</summary>
    public List<string> DebDepends { get; set; } = [];

    /// <summary>Runtime dependencies only the RPM metadata gets.</summary>
    public List<string> RpmDepends { get; set; } = [];

    /// <summary>One-line summary, used in packages (`CPACK_PACKAGE_DESCRIPTION_SUMMARY`).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Maintainer contact (`CPACK_PACKAGE_CONTACT`), required by DEB/RPM.</summary>
    public string Contact { get; set; } = string.Empty;

    ///
    public string Standard { get; set; } = "20";

    /// <summary>
    /// Gets or sets the minimum CMake policy version to use.
    /// </summary>
    /// <value>
    /// A version string (e.g., "3.5") to use for CMAKE_POLICY_VERSION_MINIMUM.
    /// This is useful for compatibility with newer CMake versions (4.0+) that have 
    /// removed support for older CMake versions used by some dependencies.
    /// </value>
    public string CmakePolicyVersion { get; set; } = string.Empty;

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
