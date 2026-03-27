using forge.Models;
using Lua;
using Lua.Standard;

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
    if (table["git"].TryRead<LuaTable>(out var gitTable))
    {
      foreach (var kvp in gitTable)
      {
        var name = kvp.Key.ToString();

        if (table[name].TryRead<LuaTable>(out var depTable))
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

    if (table["git"].TryRead<LuaValue>(out var git))
    {
      dep.Git = git.ToString();
    }

    if (table["tag"].TryRead<LuaValue>(out var tag))
    {
      dep.Tag = tag.ToString();
    }

    if (table["target"].TryRead<LuaValue>(out var target))
    {
      dep.Target = target.ToString();
    }

    return dep;
  }

  private static void ParseProjectSection(ref ProjectConfig config, LuaTable table)
  {
    if (table["name"].TryRead<LuaValue>(out var name))
    {
      config.Project.Name = name.ToString();
    }
    if (table["type"].TryRead<LuaValue>(out var type))
    {
      config.Project.Type = type.ToString();
    }
    if (table["standard"].TryRead<LuaValue>(out var standard))
    {
      config.Project.Standard = standard.ToString();
    }
    if (table["install_headers"].TryRead<LuaValue>(out var install))
    {
      config.Project.InstallHeaders = bool.TryParse(install.ToString(), out var v) && v;
    }
  }
}
