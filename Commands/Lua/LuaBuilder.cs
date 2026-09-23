using forge.CMakeGeneration;
using Lua;
using Spectre.Console;

namespace forge.Commands.Lua
{
  /// <summary>
  /// Executes Lua build scripts from the .config/forge/build/ directory.
  /// </summary>
  /// <remarks>
  /// This class runs all .lua files in the build scripts directory during
  /// application startup. A script customizes the build by returning a
  /// <c>cmakeOptions</c> table, which is emitted into the generated CMake
  /// before the project target is created — the supported way to set up
  /// something the git/local/Conan channels cannot express (an SDK download,
  /// a system package, a hand-written find module).
  /// </remarks>
  /// <example>
  /// <code>
  /// -- .config/forge/build/webgpu.lua
  /// forge.get_packages("nopass", "pacman", { "vulkan-headers" })
  /// return {
  ///   cmakeOptions = {
  ///     variables = { WEBGPU_DIR = "/opt/webgpu" },
  ///     findPackages = { "webgpu" },
  ///     linkLibraries = { "webgpu" },
  ///   }
  /// }
  /// </code>
  /// </example>
  public static class LuaBuilder
  {
    /// <summary>
    /// Runs all Lua build scripts in the .config/forge/build/ directory.
    /// </summary>
    /// <remarks>
    /// Executes each .lua file sequentially and merges any returned
    /// <c>cmakeOptions</c> table into the build context's Lua options.
    /// A script that raises a Lua error (bad argument, syntax error, failed
    /// download) is reported with its file name and stops the build: the setup
    /// it was meant to perform has not happened, so continuing would only
    /// produce a confusing failure later.
    /// </remarks>
    /// <returns>True when every script ran; false when one failed.</returns>
    public static async Task<bool> RunBuilderScripts(BuildContext context)
    {
      var buildDir = Path.Combine(Directory.GetCurrentDirectory(), ".config", "forge", "build");

      if (!Directory.Exists(buildDir))
        return true; // No scripts to run

      // Deterministic order: a setup script may depend on another's output.
      var files = Directory.GetFiles(buildDir, "*.lua");
      Array.Sort(files, StringComparer.Ordinal);

      foreach (var file in files)
      {
        var name = Path.GetFileName(file);

        LuaValue[] results;
        try
        {
          // Point the Lua API at this build before any script runs.
          LuaEngine.UseContext(context);
          results = await LuaEngine.GetLuaEngine().DoFileAsync(file);
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[bold red]Error:[/] build script '{name}' failed: {ex.Message}");
          return false;
        }

        if (results == null || results.Length == 0) continue;

        if (!results[0].TryRead<LuaTable>(out var table)) continue;
        if (table["cmakeOptions"].TryRead<LuaTable>(out var options))
          ApplyCmakeOptions(options, name, context);
      }

      return true;
    }

    private static void ApplyCmakeOptions(LuaTable options, string script, BuildContext context)
    {
      var target = context.LuaOptions;

      foreach (var (key, value) in options)
      {
        switch (key.ToString())
        {
          case "variables": ReadMap(value, target.Variables); break;
          case "cacheVariables": ReadMap(value, target.CacheVariables); break;
          case "findPackages": ReadList(value, target.FindPackages); break;
          case "includeDirs": ReadList(value, target.IncludeDirectories); break;
          case "linkDirs": ReadList(value, target.LinkDirectories); break;
          case "definitions": ReadList(value, target.Definitions); break;
          case "compileOptions": ReadList(value, target.CompileOptions); break;
          case "linkLibraries": ReadList(value, target.LinkLibraries); break;
          case "addSubdirectories": ReadList(value, target.Subdirectories); break;
          default:
            AnsiConsole.MarkupLine(
              $"[bold yellow]Warning:[/] {script}: unknown cmakeOptions key '{key}' (ignored)");
            break;
        }
      }
    }

    private static void ReadList(LuaValue value, List<string> into)
    {
      if (!value.TryRead<LuaTable>(out var table))
      {
        // A single string is accepted as a one-element list.
        var single = value.ToString();
        if (!string.IsNullOrWhiteSpace(single)) into.Add(single);
        return;
      }

      foreach (var item in table)
      {
        var text = item.Value.ToString();
        if (!string.IsNullOrWhiteSpace(text)) into.Add(text);
      }
    }

    private static void ReadMap(LuaValue value, Dictionary<string, string> into)
    {
      if (!value.TryRead<LuaTable>(out var table)) return;

      foreach (var (key, item) in table)
        into[key.ToString()] = item.ToString();
    }
  }
}
