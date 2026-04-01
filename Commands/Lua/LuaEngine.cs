using System.Text;
using forge.ForgeEngine.CoreUtils;
using forge.Models;
using Lua;
using Lua.Standard;

namespace forge.Commands.Lua
{
  /// <summary>
  /// Manages the Lua scripting engine and provides the sandbox environment for build scripts.
  /// </summary>
  /// <remarks>
  /// This class initializes and configures a Lua state with custom functions and environment
  /// information. It provides a scriptable build system that allows users to customize their
  /// build process using Lua scripts in .config/forge/build/.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Initialize the Lua engine at startup
  /// LuaEngine.InitialiseLuaEngine();
  /// 
  /// // Get the Lua state for script execution
  /// var state = LuaEngine.GetLuaEngine();
  /// </code>
  /// </example>
  public static class LuaEngine
  {
    private static LuaState _state = null!;
    private static LuaTable _forge = null!;

    /// <summary>
    /// Initializes the Lua engine with standard libraries and custom Forge functions.
    /// </summary>
    /// <remarks>
    /// This method must be called before any Lua scripts can be executed. It sets up:
    /// - Standard Lua libraries (string, math, table, etc.)
    /// - Forge-specific functions (pull_repo, get_packages, log.info)
    /// - Environment information (OS, distribution, package managers)
    /// </remarks>
    public static void InitialiseLuaEngine()
    {
      _state = LuaState.Create();
      _forge = new LuaTable();

      SetEnvironmentLibraries(ref _state);
      SetEnvironmentVariableInformation();
      RegisterModules();
    }

    /// <summary>
    /// Opens standard Lua libraries for use in scripts.
    /// </summary>
    /// <param name="state">The Lua state to configure.</param>
    private static void SetEnvironmentLibraries(ref LuaState state)
    {
      state.OpenStandardLibraries();
      state.OpenTableLibrary();
      state.OpenBasicLibrary();
      state.OpenBitwiseLibrary();
      state.OpenCoroutineLibrary();
      state.OpenDebugLibrary();
      state.OpenIOLibrary();
      state.OpenMathLibrary();
      state.OpenModuleLibrary();
      state.OpenOperatingSystemLibrary();
      state.OpenStringLibrary();
    }

    private static void RegisterModules()
    {
      var modules = new LuaFunctionModule[]
      {
        new CoreFunctionModule()
      };

      foreach (var module in modules)
      {
        module.RegisterFunctions(ref _forge);
      }

      _state.Environment["forge"] = _forge;
    }

    /// <summary>
    /// Sets up environment information tables for Lua scripts.
    /// </summary>
    /// <remarks>
    /// Populates the Lua environment with:
    /// - forge.current_working_dir - Current directory path
    /// - forge.os - Operating system information (current, windows, macos, linux)
    /// - forge.distro - Linux distribution constants and detected distro
    /// - forge.package_manager - Package manager constants
    /// </remarks>
    private static void SetEnvironmentVariableInformation()
    {
      _forge[new LuaValue("current_working_dir")] = new LuaValue(Directory.GetCurrentDirectory());

      // Operating System information
      var os = new LuaTable();
      os[new LuaValue("current")] = new LuaValue(CoreUtils.GetOperatingSystem());
      os[new LuaValue("windows")] = new LuaValue("windows");
      os[new LuaValue("macos")] = new LuaValue("macos");
      os[new LuaValue("linux")] = new LuaValue("linux");

      // Get distrobution information - useful for linux installation scripts
      var distro = new LuaTable();
      distro[new LuaValue("nixos")] = new LuaValue("nixos");
      distro[new LuaValue("fedora")] = new LuaValue("fedora");
      distro[new LuaValue("manjaro")] = new LuaValue("manjaro");
      distro[new LuaValue("arch")] = new LuaValue("arch");
      distro[new LuaValue("ubuntu")] = new LuaValue("ubuntu");
      distro[new LuaValue("debian")] = new LuaValue("debian");
      distro[new LuaValue("redhat")] = new LuaValue("redhat");
      distro[new LuaValue("unknown")] = new LuaValue("unknown");
      distro[new LuaValue("my_distro")] = new LuaValue(CoreUtils.GetLinuxDistro());

      _forge[new LuaValue("os")] = new LuaValue(os);
      _forge[new LuaValue("distro")] = new LuaValue(distro);

      // Package Managers
      var packageManager = new LuaTable();
      packageManager[new LuaValue("winget")] = new LuaValue("winget");
      packageManager[new LuaValue("chocolatey")] = new LuaValue("choco");
      packageManager[new LuaValue("brew")] = new LuaValue("brew");
      packageManager[new LuaValue("pacman")] = new LuaValue("pacman");
      packageManager[new LuaValue("aptget")] = new LuaValue("apt-get");
      packageManager[new LuaValue("no_pass")] = new LuaValue("nopass");
      _forge[new LuaValue("package_manager")] = new LuaValue(packageManager);
    }

    /// <summary>
    /// Generates and saves environment definitions for a new project.
    /// </summary>
    /// <param name="projectName">The name of the project.</param>
    /// <remarks>
    /// Creates a definitions.lua file in .config/forge/definitions/ with
    /// current environment information for the new project.
    /// </remarks>
    public static void SetEnvironmentDefinitions(string projectName)
    {
      var definitionsFilePath = Path.Combine(Directory.GetCurrentDirectory(), projectName, ".config", "forge", "definitions");

      var definitions = LuaDefinitionGenerator.GenerateDefinitions();

      File.AppendAllBytes(Path.Combine(definitionsFilePath, "definitions.lua"), Encoding.UTF8.GetBytes(definitions));
    }

    public static async Task SetConfigValue(string key, string value)
    {
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null) return;

      var parts = key.Split(' ');

      if (parts[0] == "project" && parts.Length > 1)
      {
        if (parts[1] == "name") config.Project.Name = value;
        if (parts[1] == "type") config.Project.Type = value;
        if (parts[1] == "standard") config.Project.Standard = value;

        if (parts[1] == "install_headers")
          config.Project.InstallHeaders = bool.TryParse(value, out var v) && v;

      }
      else if (parts[0] == "features" && parts.Length > 2)
      {
        var featureName = parts[1];
        var featureKey = parts[2];

        if (!config.Features.TryGetValue(featureName, out var feature))
        {
          feature = new FeatureConfig();
          config.Features[featureName] = feature;
        }

        if (featureKey == "enabled")
        {
          feature.Enabled = bool.TryParse(value, out var e) && e;
        }
        else
        {
          feature.Options[featureKey] = value;
        }
      }
      else if (parts[0] == "dependencies")
      {
        if (parts.Length == 4 && parts[1] == "git")
        {
          var depName = parts[2];
          var depProp = parts[3];

          if (!config.Dependencies.TryGetValue(depName, out var dep))
          {
            dep = new Dependency();
            config.Dependencies[depName] = dep;
          }

          if (depProp == "git") dep.Git = value;
          if (depProp == "tag") dep.Tag = value;
          if (depProp == "target") dep.Target = value;
        }
      }
      else if (parts.Length == 3 && parts[1] == "conan")
      {
        config.ConanDependencies[parts[2]] = value;
      }
    }

    public static string? GetConfigValue(ProjectConfig config, string key)
    {
      var parts = key.Split('.');

      if (parts[0] == "project")
      {
        if (parts.Length > 1 && parts[1] == "name") return config.Project.Name;
        if (parts.Length > 1 && parts[1] == "type") return config.Project.Type;
        if (parts.Length > 1 && parts[1] == "standard") return config.Project.Standard;
      }
      else if (parts[0] == "features" && parts.Length > 1)
      {
        var featureName = parts[1];

        if (config.Features.TryGetValue(featureName, out var feature))
        {
          if (parts.Length > 2 && parts[2] == "enabled")
            return feature.Enabled.ToString();

          if (parts.Length > 2 && feature.Options.TryGetValue(parts[2], out var optVal))
            return optVal;

          return feature.Enabled.ToString();
        }
      }
      else if (parts[0] == "dependencies" && parts.Length >= 3)
      {
        if (parts[0] == "git" && config.Dependencies.TryGetValue(parts[2], out var dep))
        {
          if (parts.Length > 3)
          {
            if (parts[3] == "git") return dep.Git;
            if (parts[3] == "tag") return dep.Tag;
            if (parts[3] == "target") return dep.Target;
          }
        }
        else if (parts[1] == "conan" &&
            config.ConanDependencies.TryGetValue(parts[2], out var conanVer))
        {
          return conanVer;
        }
      }

      return null;
    }

    /// <summary>
    /// Gets the initialized Lua state for script execution.
    /// </summary>
    /// <returns>The LuaState instance.</returns>
    public static LuaState GetLuaEngine() => _state;
  }
}
