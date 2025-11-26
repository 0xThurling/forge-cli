using Lua;

namespace forge.Commands.Lua
{
    public static class LuaBuilder
    {
      public static async Task RunBuilderScripts() {
        var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), ".config", "forge", "build"));

        foreach (var file in files) {
          var results = await LuaEngine.GetLuaEngine().DoFileAsync(file);
          // Read the mapped Lua Table for processing
          if (results[0].TryRead<LuaTable>(out var table))
          {
            if (table["cmakeOptions"].TryRead<LuaTable>(out var optionsTable)) {
            }
          }
        }
      }
    }
}
