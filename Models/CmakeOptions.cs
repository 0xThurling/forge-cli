namespace forge.Models
{
  /// <summary>
  /// CMake configuration contributed by Lua build scripts
  /// (<c>.config/forge/build/*.lua</c>) through the <c>cmakeOptions</c> table
  /// they return.
  /// </summary>
  /// <remarks>
  /// This is the supported channel from a custom setup script to the generated
  /// CMake: a script can install/download whatever a dependency needs, then
  /// declare the variables and targets CMake should use. The options are
  /// emitted <em>before</em> the project target is created, so
  /// <c>find_package</c>, <c>add_subdirectory</c> and variables are all
  /// available to the target and its link line.
  /// </remarks>
  /// <example>
  /// <code>
  /// return {
  ///   cmakeOptions = {
  ///     variables = { WEBGPU_DIR = "/opt/webgpu" },
  ///     findPackages = { "webgpu" },
  ///     includeDirs = { "${WEBGPU_DIR}/include" },
  ///     linkLibraries = { "webgpu" },
  ///   }
  /// }
  /// </code>
  /// </example>
  public class CmakeOptions
  {
    /// <summary>Emitted as <c>set(NAME "value")</c>.</summary>
    public Dictionary<string, string> Variables { get; } = new();

    /// <summary>
    /// Emitted as <c>set(NAME "value" CACHE STRING "" FORCE)</c>. Use these for
    /// settings a toolchain or a dependency's <c>option()</c> reads, which a
    /// plain <c>set()</c> is too late for.
    /// </summary>
    public Dictionary<string, string> CacheVariables { get; } = [];

    /// <summary>Emitted as <c>find_package(NAME REQUIRED)</c>.</summary>
    public List<string> FindPackages { get; } = [];

    /// <summary>Emitted as <c>include_directories(...)</c>.</summary>
    public List<string> IncludeDirectories { get; } = [];

    /// <summary>Emitted as <c>link_directories(...)</c>.</summary>
    public List<string> LinkDirectories { get; } = [];

    /// <summary>Emitted as <c>add_compile_definitions(...)</c>.</summary>
    public List<string> Definitions { get; } = [];

    /// <summary>Emitted as <c>add_compile_options("...")</c>.</summary>
    public List<string> CompileOptions { get; } = [];

    /// <summary>Emitted as <c>link_libraries(...)</c> (directory-scoped).</summary>
    public List<string> LinkLibraries { get; } = [];

    /// <summary>Emitted as <c>add_subdirectory(...)</c>.</summary>
    public List<string> Subdirectories { get; } = [];

    /// <summary>True when no script contributed anything.</summary>
    public bool IsEmpty =>
      Variables.Count == 0 && CacheVariables.Count == 0 && FindPackages.Count == 0 &&
      IncludeDirectories.Count == 0 && LinkDirectories.Count == 0 &&
      Definitions.Count == 0 && CompileOptions.Count == 0 &&
      LinkLibraries.Count == 0 && Subdirectories.Count == 0;

    /// <summary>Clears every contribution (called before each build).</summary>
    public void Clear()
    {
      Variables.Clear();
      FindPackages.Clear();
      IncludeDirectories.Clear();
      LinkDirectories.Clear();
      Definitions.Clear();
      CompileOptions.Clear();
      LinkLibraries.Clear();
      Subdirectories.Clear();
    }
  }
}
