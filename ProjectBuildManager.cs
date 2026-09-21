using forge.Models;

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

    /// <summary>
    /// Snippets injected after the project target and its link line
    /// (<c>forge.add_cmake(snippet)</c>).
    /// </summary>
    public static List<string> CustomCmakeSnippets { get; set; } = [];

    /// <summary>
    /// Snippets injected before the project target is created
    /// (<c>forge.add_cmake(snippet, "pre")</c>), for toolchain and SDK setup
    /// such as <c>find_package</c>, <c>add_subdirectory</c> or variables.
    /// </summary>
    public static List<string> CustomCmakeSnippetsPre { get; set; } = [];

    /// <summary>
    /// Structured CMake options returned by Lua build scripts
    /// (<c>.config/forge/build/*.lua</c> → <c>cmakeOptions</c>).
    /// </summary>
    public static CmakeOptions LuaCmakeOptions { get; set; } = new();

    /// <summary>Drops every contribution from the previous build.</summary>
    public static void ResetCustomCmake()
    {
      CustomCmakeSnippets.Clear();
      CustomCmakeSnippetsPre.Clear();
      LuaCmakeOptions.Clear();
    }
  }
}
