using Lua;

namespace cpm.Commands.Lua
{
    public class LuaBuilder : LuaEngine
    {
      public async Task RunBuilderScripts() {
        var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), ".cpm", "build"));

        foreach (var file in files) {
          var results = await state.DoFileAsync(file);
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
