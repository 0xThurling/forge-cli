using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Lua;
using Lua.Standard;
using Spectre.Console;

namespace forge.Commands.Lua
{
  public static class LuaEngine
  {
    private static LuaState _state = null!;
    private static LuaTable _cpm = null!;

    public static void InitialiseLuaEngine()
    {
      _state = LuaState.Create();
      _cpm = new LuaTable();

      SetEnvironmentLibraries(ref _state);
      SetDefinitionTables(ref _state);
    }

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

    private static void SetDefinitionTables(ref LuaState state)
    {
      SetGitFunctions();
      SetLoggingFunctionsAndDefinitions();
      SetEnvironmentVariableInformation();
      SetGetPackagesFunction();

      state.Environment["forge"] = _cpm;
    }

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

    private static void InstallPackages(bool hasPass, string packageManager, List<string> packages, string? pass = null) {
      string command = 
        packageManager switch {
          "brew" or "winget" or "apt-get" or "choco" => " install ",
          "pacman" => " -S ",
          _ => " "
        } +
        $"{string.Join(" ", packages)}" + 
        packageManager switch {
          "pacman" => " --noconfirm ",
          _ => string.Empty
        };


        var installationProcess = new ProcessStartInfo();
        if (hasPass && packageManager switch {"brew" or "apt-get" or "pacman" => true, _ => false})
        {
          installationProcess = new ProcessStartInfo("sudo", $"-S {packageManager} {command}") {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
          };
        } else {
          installationProcess = new ProcessStartInfo($"{packageManager}", $"{command}") {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
          };
        }

        using var process = Process.Start(installationProcess); 
        
        AnsiConsole.WriteLine("Installing packages...");

        process!.WaitForExit();
    }

    private static void SetGetPackagesFunction() {
      var getPackagesFunc = new LuaFunction("get_packages", async (context, token) =>
      {
        var password = context.GetArgument<string>(0);
        var packageManager = context.GetArgument<string>(1);
        var packages = context.GetArgument<LuaTable>(2);

        var packageList = packages.Select(x => x.Value.ToString()).ToList();

        if (password == "nopass")
        {
          InstallPackages(false, packageManager, packageList);
        } else {
          InstallPackages(true, packageManager, packageList, password);
        }
  
        return 0;
      });

      _cpm[new LuaValue("get_packages")] = new LuaValue(getPackagesFunc);
    }

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

    private static string GetLinuxDistro() {
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

        return dict.TryGetValue("NAME", out var name) ? name : "unknown";
    }

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

    public static void SetEnvironmentDefinitions(string projectName)
    {
      var definitionsFilePath = Path.Combine(Directory.GetCurrentDirectory(), projectName, ".config", "forge", "definitions");

      var definitions = LuaDefinitionGenerator.GenerateDefinitions();

      File.AppendAllBytes(Path.Combine(definitionsFilePath, "definitions.lua"), Encoding.UTF8.GetBytes(definitions));
    }

    private static string GetOperatingSystem() {
      if (OperatingSystem.IsLinux()) return "linux";
      if (OperatingSystem.IsMacOS()) return "macos";
      if (OperatingSystem.IsWindows()) return "windows";
      return string.Empty;
    }

    public static LuaState GetLuaEngine() => _state;
  }
}
