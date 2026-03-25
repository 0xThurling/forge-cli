using forge.Models;
using Lua;
using Lua.Standard;

namespace forge.Commands.Lua;

public class LuaConfigLoader
{
  private LuaState? _state;
  
  public async Task<ProjectConfig?> LoadConfig() {
    if (File.Exists("forge.lua"))
    {
      return await LoadFromLuaAsync("forge.lua");
    }

    if (File.Exists("package.toml"))
    {
      return LoadFromToml();
    }

    return null;
  }

  private static ProjectConfig? LoadFromToml() {
    return ProjectConfigManager.LoadConfig();
  }

  private async Task<ProjectConfig> LoadFromLuaAsync(string filePath)
  {
    _state = LuaState.Create();
    _state.OpenStandardLibraries();

    var result = await _state.DoFileAsync(filePath);

    // Read the mapped Lua Table for processing
    if (result != null &&
        result?.Length == 0 &&
        result[0].TryRead<LuaTable>(out var table))
    {
      return ParseLuaTable(table);
    }

    throw new Exception("forge.lua must return a table");
  }

  private ProjectConfig ParseLuaTable(LuaTable table) {
    var config = new ProjectConfig();

    // Parse Project Sections
    if (table["project"].TryRead<LuaTable>(out var projectTable))
    {
       config.Project = ParseProjectSection(projectTable); 
    }

    // Parse Dependencies
    if (table["project"].TryRead<LuaTable>(out var projectTable))
    {
       config.Project = ParseProjectSection(projectTable); 
    }
  }

    private ProjectSection ParseProjectSection(LuaTable projectTable)
    {
        throw new NotImplementedException();
    }
}
