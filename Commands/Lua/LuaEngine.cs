using System.Text;
using Lua;
using Lua.Standard;
using Spectre.Console;

namespace cpm.Commands.Lua
{
  public static class LuaEngine
  {
    private static LuaState state = default!;

    public static void InitialiseLuaEngine() {
      state = LuaState.Create();
      
      SetEnvironmentLibraries(ref state);
      SetDefinitionTables(ref state);
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

    private static void SetDefinitionTables(ref LuaState state) {
      var cpm = new LuaTable();
      var log = new LuaTable();

      var logInformationFunc = new LuaFunction("info", async (context, token) =>
      {
        var info = context.GetArgument<string>(0);
        AnsiConsole.MarkupLine($"[bold blue]INFO[/]: {info}");
        return 0;
      }); 

      log[new LuaValue("info")] = new LuaValue(logInformationFunc);
      cpm[new LuaValue("log")] = new LuaValue(log);
      cpm[new LuaValue("current_working_dir")] = new LuaValue(Directory.GetCurrentDirectory());

      state.Environment["cpm"] = cpm;
    }

    public static void SetEnvironmentDefinitions(string projectName) {
        var definitionsFilePath = Path.Combine(Directory.GetCurrentDirectory(), projectName,".config", "cpm", "definitions");

        var definitions = LuaDefinitionGenerator.GenerateDefinitions();

        File.AppendAllBytes(Path.Combine(definitionsFilePath, "definitions.lua"), Encoding.UTF8.GetBytes(definitions));
    }

    public static LuaState GetLuaEngine() => state;
  }
}
