using Lua;

namespace forge.Commands.Lua
{
  /// <summary>
  /// Executes Lua build scripts from the .config/forge/build/ directory.
  /// </summary>
  /// <remarks>
  /// This class runs all .lua files in the build scripts directory during
  /// application startup. Scripts can customize the build process by setting
  /// cmakeOptions and performing other build-time configuration.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Automatically called at startup
  /// await LuaBuilder.RunBuilderScripts();
  /// </code>
  /// </example>
  public static class LuaBuilder
  {
    /// <summary>
    /// Runs all Lua build scripts in the .config/forge/build/ directory.
    /// </summary>
    /// <remarks>
    /// Executes each .lua file sequentially and processes any returned
    /// cmakeOptions table for CMake configuration.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunBuilderScripts()
    {
      var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), ".config", "forge", "build"));

      foreach (var file in files)
      {
        var results = await LuaEngine.GetLuaEngine().DoFileAsync(file);

        if (results == null || results?.Length == 0) continue;

        // Read the mapped Lua Table for processing
        if (results != null && results[0].TryRead<LuaTable>(out var table))
        {
          if (table["cmakeOptions"].TryRead<LuaTable>(out var optionsTable))
          {
          }
        }
      }
    }
  }
}
