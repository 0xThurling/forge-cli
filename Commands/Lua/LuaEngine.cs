using Lua;
using Lua.Standard;

namespace cpm.Commands.Lua
{
  public class LuaEngine
  {
    public LuaState state;

    public LuaEngine()
    {
      state = LuaState.Create();

      SetEnvironmentLibraries(ref state);
      SetEnvironmentVariablesForSandbox(ref state);
      SetEnvironmentFunctions(ref state);
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

    private static void SetEnvironmentVariablesForSandbox(ref LuaState state)
    {
      state.Environment["currentWorkingDirectory"] = Directory.GetCurrentDirectory();
    }


    private static void SetEnvironmentFunctions(ref LuaState state)
    {
      // Define a function that waits for the given number of seconds using Task.Delay
      state.Environment["wait"] = new LuaFunction(async (context, token) =>
      {
        var sec = context.GetArgument<double>(0);
        await Task.Delay(TimeSpan.FromSeconds(sec));
        return 0;
      });
    }
  }
}
