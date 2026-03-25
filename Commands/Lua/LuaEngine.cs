using System.Diagnostics;
using System.Text;
using Lua;
using Lua.Standard;
using Spectre.Console;

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
    private static LuaTable _cpm = null!;

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
      _cpm = new LuaTable();

      SetEnvironmentLibraries(ref _state);
      SetDefinitionTables(ref _state);
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

    /// <summary>
    /// Sets up Forge-specific Lua functions and environment tables.
    /// </summary>
    /// <param name="state">The Lua state to configure.</param>
    private static void SetDefinitionTables(ref LuaState state)
    {
      SetGitFunctions();
      SetLoggingFunctionsAndDefinitions();
      SetEnvironmentVariableInformation();
      SetGetPackagesFunction();
      SetCustomCMakeFunctions();

      state.Environment["forge"] = _cpm;
    }

    /// <summary>
    /// Registers the pull_repo function for cloning Git repositories.
    /// </summary>
    /// <remarks>
    /// Provides forge.pull_repo(url) function that clones a Git repository
    /// to the external/ directory.
    /// </remarks>
    private static void SetGitFunctions()
    {
      var pullRepoFunc = new LuaFunction("pull_repo", async (context, token) =>
      {
        var repoUrl = context.GetArgument<string>(0);

        var repoName = repoUrl.Split('/')[^1].Split(".")[0];

        var processStartInfo = new ProcessStartInfo("git", $"clone {repoUrl} external/{repoName}")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };

        using var process = Process.Start(processStartInfo) ?? throw new Exception("Failed to pull repository");

        AnsiConsole.WriteLine("Cloning repo...");

        await process.WaitForExitAsync(token);

        return process.ExitCode;
      });

      _cpm[new LuaValue("pull_repo")] = new LuaValue(pullRepoFunc);
    }

    /// <summary>
    /// Installs system packages using the specified package manager.
    /// </summary>
    /// <param name="hasPass">Whether sudo password is required.</param>
    /// <param name="packageManager">The package manager to use.</param>
    /// <param name="packages">List of package names to install.</param>
    /// <param name="pass">Optional sudo password.</param>
    private static void InstallPackages(bool hasPass, string packageManager, List<string> packages, string? pass = null)
    {
      string command =
        packageManager switch
        {
          "brew" or "winget" or "apt-get" or "choco" => " install ",
          "pacman" => " -S ",
          _ => " "
        } +
        $"{string.Join(" ", packages)}" +
        packageManager switch
        {
          "pacman" => " --noconfirm ",
          _ => string.Empty
        };


      var installationProcess = new ProcessStartInfo();
      if (hasPass && packageManager switch { "brew" or "apt-get" or "pacman" => true, _ => false })
      {
        installationProcess = new ProcessStartInfo("sudo", $"-S {packageManager} {command}")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };
      }
      else
      {
        installationProcess = new ProcessStartInfo($"{packageManager}", $"{command}")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };
      }

      using var process = Process.Start(installationProcess);

      if (process == null) throw new Exception("Failed to start CMake process.");
      AnsiConsole.WriteLine("Installing packages...");

      process!.WaitForExit();
    }

    /// <summary>
    /// Registers the get_packages function for installing system packages.
    /// </summary>
    /// <remarks>
    /// Provides forge.get_packages(password, manager, packages) function for
    /// cross-platform package installation.
    /// </remarks>
    private static void SetGetPackagesFunction()
    {
      var getPackagesFunc = new LuaFunction("get_packages", async (context, token) =>
      {
        var password = context.GetArgument<string>(0);
        var packageManager = context.GetArgument<string>(1);
        var packages = context.GetArgument<LuaTable>(2);

        var packageList = packages.Select(x => x.Value.ToString()).ToList();

        if (password == "nopass")
        {
          InstallPackages(false, packageManager, packageList);
        }
        else
        {
          InstallPackages(true, packageManager, packageList, password);
        }

        return 0;
      });

      _cpm[new LuaValue("get_packages")] = new LuaValue(getPackagesFunc);
    }

    /// <summary>
    /// Registers logging functions for Lua scripts.
    /// </summary>
    /// <remarks>
    /// Provides forge.log.info(message) function for displaying
    /// informational messages in the terminal.
    /// </remarks>
    private static void SetLoggingFunctionsAndDefinitions()
    {
      var log = new LuaTable();

      var logInformationFunc = new LuaFunction("info", (context, _) =>
      {
        var info = context.GetArgument<string>(0);
        AnsiConsole.MarkupLine($"[bold blue]INFO[/]: {info}");
        return ValueTask.FromResult(0);
      });

      log[new LuaValue("info")] = new LuaValue(logInformationFunc);
      _cpm[new LuaValue("log")] = new LuaValue(log);
    }

    /// <summary>
    /// Detects the current Linux distribution from /etc/os-release.
    /// </summary>
    /// <returns>The canonical distribution name (e.g., "ubuntu", "arch", "fedora").</returns>
    private static string GetLinuxDistro()
    {
      var dict = new Dictionary<string, string>();
      try
      {
        var lines = File.ReadAllLines("/etc/os-release");

        foreach (var line in lines)
        {
          if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

          var parts = line.Split('=', 2);
          if (parts.Length == 2)
          {
            var key = parts[0].Trim();
            var value = parts[1].Trim().Trim('"'); // remove quotes
            dict[key] = value;
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("Error reading distro info: " + ex.Message);
        return "unknown";
      }

      return dict.TryGetValue("NAME", out var name) ? DistroName(name) : "unknown";
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
      _cpm[new LuaValue("current_working_dir")] = new LuaValue(Directory.GetCurrentDirectory());

      // Operating System information
      var os = new LuaTable();
      os[new LuaValue("current")] = new LuaValue(GetOperatingSystem());
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
      distro[new LuaValue("my_distro")] = new LuaValue(GetLinuxDistro());

      _cpm[new LuaValue("os")] = new LuaValue(os);
      _cpm[new LuaValue("distro")] = new LuaValue(distro);

      // Package Managers
      var packageManager = new LuaTable();
      packageManager[new LuaValue("winget")] = new LuaValue("winget");
      packageManager[new LuaValue("chocolatey")] = new LuaValue("choco");
      packageManager[new LuaValue("brew")] = new LuaValue("brew");
      packageManager[new LuaValue("pacman")] = new LuaValue("pacman");
      packageManager[new LuaValue("aptget")] = new LuaValue("apt-get");
      packageManager[new LuaValue("no_pass")] = new LuaValue("nopass");
      _cpm[new LuaValue("package_manager")] = new LuaValue(packageManager);
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

    /// <summary>
    /// Detects the current operating system.
    /// </summary>
    /// <returns>"linux", "macos", or "windows".</returns>
    private static string GetOperatingSystem()
    {
      if (OperatingSystem.IsLinux()) return "linux";
      if (OperatingSystem.IsMacOS()) return "macos";
      if (OperatingSystem.IsWindows()) return "windows";
      return string.Empty;
    }

    private static void SetCustomCMakeFunctions()
    {
      var addCmakeFunc = new LuaFunction("add_cmake", (context, token) =>
      {
        var cmakeSnippet = context.GetArgument<string>(0);

        ProjectBuildManager.CustomCmakeSnippets.Add(cmakeSnippet);

        AnsiConsole.MarkupLine($"[green]Added custome CMake snippet[/]");

        return ValueTask.FromResult(0);
      });

      _cpm[new LuaValue("add_cmake")] = new LuaValue(addCmakeFunc);
    }

    /// <summary>
    /// Gets the initialized Lua state for script execution.
    /// </summary>
    /// <returns>The LuaState instance.</returns>
    public static LuaState GetLuaEngine() => _state;

    private static string DistroName(string name) => name switch
    {
      var n when n.Contains("arch", StringComparison.OrdinalIgnoreCase) => "arch",
      var n when n.Contains("debian", StringComparison.OrdinalIgnoreCase) => "debian",
      var n when n.Contains("ubuntu", StringComparison.OrdinalIgnoreCase) => "ubuntu",
      var n when n.Contains("mint", StringComparison.OrdinalIgnoreCase) => "mint",
      var n when n.Contains("kali", StringComparison.OrdinalIgnoreCase) => "kali",
      var n when n.Contains("red hat", StringComparison.OrdinalIgnoreCase) => "redhat",
      var n when n.Contains("fedora", StringComparison.OrdinalIgnoreCase) => "fedora",
      var n when n.Contains("centos", StringComparison.OrdinalIgnoreCase) => "centos",
      var n when n.Contains("rocky", StringComparison.OrdinalIgnoreCase) => "rocky",
      var n when n.Contains("manjaro", StringComparison.OrdinalIgnoreCase) => "manjaro",
      var n when n.Contains("garuda", StringComparison.OrdinalIgnoreCase) => "garuda",
      var n when n.Contains("alpine", StringComparison.OrdinalIgnoreCase) => "alpine",
      var n when n.Contains("amazon", StringComparison.OrdinalIgnoreCase) => "amazon",
      var n when n.Contains("nixos", StringComparison.OrdinalIgnoreCase) => "nixos",
      _ => "unknown"
    };
  }
}
