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
    }

    public static void SetEnvironmentDefinitions(string projectName)
    {
      var definitionsFilePath = Path.Combine(Directory.GetCurrentDirectory(), projectName, ".config", "forge", "definitions");

      var definitions = LuaDefinitionGenerator.GenerateDefinitions();

      File.AppendAllBytes(Path.Combine(definitionsFilePath, "definitions.lua"), Encoding.UTF8.GetBytes(definitions));
    }

    public static LuaState GetLuaEngine() => _state;
  }
}
