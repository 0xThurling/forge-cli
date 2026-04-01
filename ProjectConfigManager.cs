using System.Text;
using forge.Commands.Lua;
using forge.Models;
using Spectre.Console;
using Tommy;

namespace forge
{
  /// <summary>
  /// Manages the reading and writing of project configuration stored in forge.lua
  /// </summary>
  /// <remarks>
  /// This class provides static methods to load project configuration from the forge.lua
  /// file, save configuration changes, and locate the project root directory. It uses the
  /// Tommy library for TOML parsing and provides a clean interface for accessing project
  /// configuration throughout the application.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Load configuration
  /// var config = ProjectConfigManager.LoadConfig();
  /// if (config != null)
  /// {
  ///     Console.WriteLine($"Project: {config.Project.Name}");
  /// }
  /// 
  /// // Save configuration
  /// var newConfig = new ProjectConfig { ... };
  /// ProjectConfigManager.SaveConfig(newConfig);
  /// </example>
  public static class ProjectConfigManager
  {
    /// <summary>
    /// The name of the package configuration file that identifies a Forge project.
    /// </summary>
    private const string ConfigFileName = "forge.lua";
    private const string LegacyConfigFileName = "package.toml";

    private static readonly LuaConfigLoader _luaLoader = new();

    /// <summary>
    /// Loads and parses the forge.lua configuration file from the current project directory.
    /// </summary>
    /// <returns>
    /// A <see cref="ProjectConfig"/> object containing the parsed configuration, or null if
    /// the forge.lua file cannot be found or parsed.
    /// </returns>
    /// <remarks>
    /// This method searches for forge.lua by walking up the directory tree starting from
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = ProjectConfigManager.LoadConfig();
    /// if (config != null)
    /// {
    ///     Console.WriteLine($"Loading {config.Project.Name}");
    /// }
    /// </example>
    public static async Task<ProjectConfig?> LoadConfigAsync()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null)
      {
        return null;
      }

      var configPath = Path.Combine(projectRoot, ConfigFileName);

      return await _luaLoader.LoadConfig(configPath);
    }

    /// <summary>
    /// Finds the root directory of the current Forge project by searching for package.toml.
    /// </summary>
    /// <returns>
    /// The absolute path to the project root directory, or null if no package.toml file
    /// is found in the current directory or any parent directory.
    /// </returns>
    /// <remarks>
    /// This method traverses the directory tree upward from the current working directory,
    /// checking each level for the presence of package.toml. The first directory containing
    /// package.toml is considered the project root.
    /// </remarks>
    /// <example>
    /// <code>
    /// var root = ProjectConfigManager.FindProjectRoot();
    /// if (root != null)
    /// {
    ///     Console.WriteLine($"Project root: {root}");
    /// }
    /// </example>
    public static string? FindProjectRoot(string fileName = ConfigFileName)
    {
      var currentDir = Directory.GetCurrentDirectory();
      while (currentDir != null)
      {
        if (File.Exists(Path.Combine(currentDir, fileName)))
        {
          return currentDir;
        }
        currentDir = Directory.GetParent(currentDir)?.FullName;
      }
      return null;
    }

    public static ProjectConfig? TomlConfigLoader()
    {
      var projectRoot = FindProjectRoot(LegacyConfigFileName);
      if (projectRoot == null)
      {
        return null;
      }

      var configPath = Path.Combine(projectRoot, LegacyConfigFileName);

      try
      {
        using var reader = new StreamReader(File.OpenRead(configPath));
        var toml = TOML.Parse(reader);

        var config = new ProjectConfig
        {
          Project = new ProjectSection
          {
            Name = toml["project"]["name"],
            Type = toml["project"]["type"],
            Standard = toml["project"].HasKey("standard") ? toml["project"]["standard"] : "",
            InstallHeaders = toml["project"].HasKey("install_headers") ? toml["project"]["install_headers"] : false,
          },
          Dependencies = []
        };

        if (toml.HasKey("dependencies"))
        {
          var depsTable = toml["dependencies"].AsTable;
          foreach (var key in depsTable.Keys)
          {
            var depTable = depsTable[key];
            config.Dependencies.Add(key, new Dependency
            {
              Git = depTable["git"],
              Tag = depTable["tag"],
              Target = depTable.HasKey("target") ? depTable["target"] : ""
            });
          }
        }

        if (toml.HasKey("conan-dependencies"))
        {
          var depsTable = toml["conan-dependencies"].AsTable;
          foreach (var key in depsTable.Keys)
          {
            config.ConanDependencies.Add(key, depsTable[key]);
          }
        }

        if (toml.HasKey("resources") && toml["resources"].HasKey("files"))
        {
          var filesArray = toml["resources"]["files"].AsArray;
          config.Resources.Files = [.. filesArray.RawArray.Select(node => node.ToString()).Where(s => s != null).Select(s => s!)];
        }

        if (toml.HasKey("scripts"))
        {
          var scriptsTable = toml["scripts"].AsTable;
          foreach (var key in scriptsTable.Keys)
          {
            config.Scripts.Add(key, scriptsTable[key]);
          }
        }

        return config;
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[bold red]Error reading {LegacyConfigFileName}:[/] {ex.Message}");
        return null;
      }
    }

    /// <summary>
    /// Saves the project configuration to package.toml file.
    /// </summary>
    /// <param name="config">The <see cref="ProjectConfig"/> object to serialize to TOML format.</param>
    /// <remarks>
    /// This method serializes the provided ProjectConfig object to TOML format and writes it
    /// to the package.toml file in the specified directory. Only sections with data are written:
    /// dependencies, resources, and scripts are only included if they contain entries.
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = new ProjectConfig
    /// {
    ///     Project = new ProjectSection { Name = "myapp", Type = "executable" }
    /// };
    /// ProjectConfigManager.SaveConfig(config);
    /// </example>
    public static void SaveConfig(ProjectConfig config)
    {
      var luaContent = SerializeToLua(config);
      File.WriteAllText(ConfigFileName, luaContent);
    }

    public static string SerializeToLua(ProjectConfig config)
    {
      var sb = new StringBuilder();
      sb.AppendLine("return {");
      // Project section - always output
      sb.AppendLine("    project = {");
      sb.AppendLine($"        name = \"{config.Project.Name}\",");
      sb.AppendLine($"        type = \"{config.Project.Type}\",");
      sb.AppendLine($"        standard = \"{config.Project.Standard}\",");
      if (config.Project.InstallHeaders)
      {
        sb.AppendLine("        install_headers = true,");
      }
      sb.AppendLine("    },");
      // Dependencies section - always output structure
      sb.AppendLine("    dependencies = {");

      if (config.Dependencies.Count > 0)
      {
        sb.AppendLine("        direct = {");
        foreach (var dep in config.Dependencies)
        {
          sb.AppendLine($"            {dep.Key} = {{");
          if (!string.IsNullOrEmpty(dep.Value.Git))
            sb.AppendLine($"                git = \"{dep.Value.Git}\",");
          if (!string.IsNullOrEmpty(dep.Value.Tag))
            sb.AppendLine($"                tag = \"{dep.Value.Tag}\",");
          if (!string.IsNullOrEmpty(dep.Value.Target))
            sb.AppendLine($"                target = \"{dep.Value.Target}\",");
          sb.AppendLine("            },");
        }
        sb.AppendLine("        },");
      }
      else
      {
        sb.AppendLine("        direct = {},");
      }
      if (config.ConanDependencies.Count > 0)
      {
        sb.AppendLine("        conan = {");
        foreach (var dep in config.ConanDependencies)
        {
          sb.AppendLine($"            {dep.Key} = \"{dep.Value}\",");
        }
        sb.AppendLine("        },");
      }
      else
      {
        sb.AppendLine("        conan = {},");
      }
      sb.AppendLine("    },");
      // Resources section - always output
      sb.AppendLine("    resources = {");
      if (config.Resources.Files.Count > 0)
      {
        sb.AppendLine("        files = {");
        foreach (var file in config.Resources.Files)
        {
          sb.AppendLine($"            \"{file}\",");
        }
        sb.AppendLine("        },");
      }
      else
      {
        sb.AppendLine("        files = {},");
      }
      sb.AppendLine("    },");
      // Scripts section - always output
      if (config.Scripts.Count > 0)
      {
        sb.AppendLine("    scripts = {");
        foreach (var script in config.Scripts)
        {
          sb.AppendLine($"        [\"{script.Key}\"] = \"{script.Value}\",");
        }
        sb.AppendLine("    },");
      }
      else
      {
        sb.AppendLine("    scripts = {},");
      }
      // Features section - always output with proper table format
      sb.AppendLine("    features = {");
      if (config.Features.Count > 0)
      {
        foreach (var (name, feature) in config.Features)
        {
          // If feature has options, output as table with "enabled" key
          if (feature.Options.Count > 0)
          {
            sb.AppendLine($"        {name} = {{");
            sb.AppendLine($"            enabled = {feature.Enabled.ToString().ToLower()},");
            foreach (var (optKey, optVal) in feature.Options)
            {
              sb.AppendLine($"            {optKey} = \"{optVal}\",");
            }
            sb.AppendLine("        },");
          }
          else
          {
            // Simple boolean format
            sb.AppendLine($"        {name} = {feature.Enabled.ToString().ToLower()},");
          }
        }
      }
      sb.AppendLine("    },");
      // Custom section - output if present
      if (config.Custom.Count > 0)
      {
        sb.AppendLine("    custom = {");
        foreach (var kvp in config.Custom)
        {
          var key = kvp.Key.Contains('.') ? $"[\"{kvp.Key}\"]" : kvp.Key;
          sb.AppendLine($"        {key} = \"{kvp.Value}\",");
        }
        sb.AppendLine("    },");
      }
      sb.AppendLine("}");
      return sb.ToString();
    }
    /// <summary>
    /// Gets the name of the current project from the package.toml configuration.
    /// </summary>
    /// <returns>
    /// The project name as defined in package.toml [project] section, or null if the
    /// configuration cannot be loaded.
    /// </returns>
    /// <remarks>
    /// This is a convenience method that combines FindProjectRoot() and LoadConfig() to
    /// quickly retrieve just the project name without loading the full configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// var name = ProjectConfigManager.GetProjectName();
    /// Console.WriteLine($"Building {name}");
    /// </example>
    public static async Task<string?> GetProjectName()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null) return null;
      var config = await LoadConfigAsync();
      return config?.Project?.Name;
    }
  }
}
