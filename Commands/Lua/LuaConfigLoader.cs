using forge.Models;
using Lua;
using Lua.Standard;
using Spectre.Console;

namespace forge.Commands.Lua;

public class LuaConfigLoader
{
  private LuaState? _state;

  public async Task<ProjectConfig?> LoadConfig(string path)
  {
    if (File.Exists(path))
    {
      return await LoadFromLuaAsync(path);
    }

    return null;
  }

  private async Task<ProjectConfig?> LoadFromLuaAsync(string filePath)
  {
    _state = LuaState.Create();
    _state.OpenStandardLibraries();

    try
    {
      var result = await _state.DoFileAsync(filePath);

      // Read the mapped Lua Table for processing
      if (result != null &&
          result?.Length > 0 &&
          result[0].TryRead<LuaTable>(out var table))
      {
        return ParseLuaTable(table);
      }
    }
    catch (LuaCompileException compileException)
    {
      AnsiConsole.MarkupLine($"[red]Error loading [bold white]forge.lua[/] config file[/]: {compileException.MainMessage}");
      return null;
    }

    AnsiConsole.MarkupLine($"[red][bold white]forge.lua[/] must be a table.[/]");
    return null;
  }

  private static ProjectConfig ParseLuaTable(LuaTable table)
  {
    var config = new ProjectConfig();

    // Parse Project Sections
    if (table["project"].TryRead<LuaTable>(out var projectTable))
    {
      ParseProjectSection(ref config, projectTable);
    }

    // Parse Dependencies
    if (table["dependencies"].TryRead<LuaTable>(out var dependenciesTable))
    {
      ParseDependencies(ref config, dependenciesTable);
    }

    // Parse Resources 
    if (table["resources"].TryRead<LuaTable>(out var resourcesTable))
    {
      ParseResources(ref config, resourcesTable);
    }

    // Parse Scripts 
    if (table["scripts"].TryRead<LuaTable>(out var scriptsTable))
    {
      ParseScripts(ref config, scriptsTable);
    }

    // Parse Features 
    if (table["features"].TryRead<LuaTable>(out var featuresTable))
    {
      ParseFeatures(ref config, featuresTable);
    }

    // Check if testing is enabled
    if (table["testing"] != LuaValue.Nil)
    {
      config.Testing = bool.TryParse(table["testing"].ToString(), out var t) && t;
    }

    // Parse Custom section
    if (table["custom"].TryRead<LuaTable>(out var customTable))
    {
      foreach (var kvp in customTable)
      {
        var key = kvp.Key.ToString();
        var value = kvp.Value.ToString();
        config.Custom[key] = value;
      }
    }

    // Parse any remaining top-level keys
    foreach (var kvp in table)
    {
      var key = kvp.Key.ToString();

      // Skip known sections
      if (key is "project" or "dependencies" or "resources" or "scripts" or "features" or "custom")
        continue;

      var value = kvp.Value.ToString();
      config.Custom[key] = value;
    }

    return config;
  }

  private static void ParseFeatures(ref ProjectConfig config, LuaTable table)
  {
    foreach (var kvp in table)
    {
      var name = kvp.Key.ToString();

      if (kvp.Value.TryRead<LuaTable>(out var featureTable))
      {
        var featureConfig = new FeatureConfig();

        if (featureTable["enabled"].TryRead<LuaValue>(out var enabled))
        {
          featureConfig.Enabled = bool.TryParse(enabled.ToString(), out var e) && e;
        }

        foreach (var optKvp in featureTable)
        {
          var optName = optKvp.Key.ToString();
          var optValue = optKvp.Value.ToString();

          if (optName != "enabled")
          {
            featureConfig.Options[optName] = optValue;
          }
        }

        config.Features[name] = featureConfig;
      }
      else
      {
        config.Features[name] = new FeatureConfig
        {
          Enabled = bool.TryParse(kvp.Value.ToString(), out var e) && e
        };
      }
    }
  }

  private static void ParseScripts(ref ProjectConfig config, LuaTable table)
  {
    foreach (var kvp in table)
    {
      var name = kvp.Key.ToString();
      var script = kvp.Value.ToString();

      config.Scripts[name] = script;
    }
  }

  private static void ParseResources(ref ProjectConfig config, LuaTable table)
  {

    if (table["files"].TryRead<LuaTable>(out var filesTable))
    {
      foreach (var kvp in filesTable)
      {
        var file = kvp.Value.ToString();
        config.Resources.Files.Add(file);
      }
    }
  }

  private static void ParseDependencies(ref ProjectConfig config, LuaTable table)
  {
    if (table["direct"].TryRead<LuaTable>(out var directTable))
    {
      foreach (var kvp in directTable)
      {
        var name = kvp.Key.ToString();

        if (directTable[name].TryRead<LuaTable>(out var depTable))
        {
          config.Dependencies[name] = ParseDependencyFromTable(depTable);
        }
      }
    }

    if (table["conan"].TryRead<LuaTable>(out var conanTable))
    {
      foreach (var kvp in conanTable)
      {
        var name = kvp.Key.ToString();
        var version = kvp.Value.ToString();

        config.ConanDependencies[name] = version;
      }
    }
  }

  private static Dependency ParseDependencyFromTable(LuaTable table)
  {
    var dep = new Dependency();

    if (table["git"] != LuaValue.Nil)
    {
      dep.Git = table["git"].ToString();
    }

    if (table["tag"] != LuaValue.Nil)
    {
      dep.Tag = table["tag"].ToString();
    }

    if (table["target"] != LuaValue.Nil)
    {
      dep.Target = table["target"].ToString();
    }

    return dep;
  }

  private static void ParseProjectSection(ref ProjectConfig config, LuaTable table)
  {
    if (table["name"] != LuaValue.Nil)
    {
      config.Project.Name = table["name"].ToString();
    }
    if (table["type"] != LuaValue.Nil)
    {
      config.Project.Type = table["type"].ToString();
    }
    if (table["standard"] != LuaValue.Nil)
    {
      config.Project.Standard = table["standard"].ToString();
    }
    if (table["install_headers"] != LuaValue.Nil)
    {
      config.Project.InstallHeaders = bool.TryParse(table["install_headers"].ToString(), out var v) && v;
    }
  }
}
