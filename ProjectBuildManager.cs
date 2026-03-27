namespace forge
{
  /// <summary>
  /// Manages build-time state information that needs to be shared between different
  /// command executions during the build process.
  /// </summary>
  /// <remarks>
  /// This class serves as a bridge between the Conan install phase and the CMake build phase.
  /// When Conan dependencies are installed, their CMake target information is parsed and stored
  /// in static lists. These lists are then read during CMake generation to properly link the
  /// dependencies to the project target.
  /// </remarks>
  /// <example>
  /// <code>
  /// // After Conan install, dependencies are stored:
  /// ProjectBuildManager.LinkDependencies.Add("fmt::fmt");
  /// ProjectBuildManager.FindDependencies.Add("fmt");
  /// 
  /// // During build, these are read:
  /// foreach (var dep in ProjectBuildManager.LinkDependencies) { ... }
  /// </example>
  public static class ProjectBuildManager
  {
    /// <summary>
    /// Stores CMake target names from Conan packages that should be passed to target_link_libraries().
    /// </summary>
    /// <value>
    /// A list of CMake target strings (e.g., "fmt::fmt", "spdlog::spdlog").
    /// </value>
    /// <remarks>
    /// These targets are extracted from Conan's CMake output during the install phase and
    /// used when generating the CMakeLists.txt file to properly link the libraries.
    /// </remarks>
    public static List<string> LinkDependencies { get; set; } = [];

    /// <summary>
    /// Stores CMake module names from Conan packages that should be passed to find_package().
    /// </summary>
    /// <value>
    /// A list of package names that need to be found (e.g., "fmt", "spdlog").
    /// </value>
    /// <remarks>
    /// These module names are extracted from Conan's CMake output and used to generate
    /// find_package() calls in the CMakeLists.txt file.
    /// </remarks>
    public static List<string> FindDependencies { get; set; } = [];

    public static List<string> CustomCmakeSnippets { get; set; } = [];
  }
}
