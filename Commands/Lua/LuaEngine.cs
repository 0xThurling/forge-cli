using System.Diagnostics;
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

    private static void SetGetPackagesFunction() {
      var getPackagesFunc = new LuaFunction("get_packages", async (context, token) =>
      {
        var packageManager = context.GetArgument<string>(0);
        var packages = context.GetArgument<string[]>(1);

        foreach (var package in packages) {
          AnsiConsole.WriteLine(package);
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

    private static void SetEnvironmentVariableInformation()
    {
      _cpm[new LuaValue("current_working_dir")] = new LuaValue(Directory.GetCurrentDirectory());

      // Operating System information
      var os = new LuaTable();
      os[new LuaValue("current")] = new LuaValue(GetOperatingSystem());
      os[new LuaValue("windows")] = new LuaValue("windows");
      os[new LuaValue("macos")] = new LuaValue("macos");
      os[new LuaValue("linux")] = new LuaValue("linux");
      _cpm[new LuaValue("os")] = new LuaValue(os);

      // Package Managers
      var packageManager = new LuaTable(); 
      packageManager[new LuaValue("winget")] = new LuaValue("winget");
      packageManager[new LuaValue("chocolatey")] = new LuaValue("choco");
      packageManager[new LuaValue("brew")] = new LuaValue("brew");
      packageManager[new LuaValue("pacman")] = new LuaValue("pacman");
      packageManager[new LuaValue("aptget")] = new LuaValue("aptget");
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
