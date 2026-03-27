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
    public static ProjectConfig? LoadConfig()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null)
      {
        return null;
      }

      var configPath = Path.Combine(projectRoot, ConfigFileName);

      try
      {
        using (var reader = new StreamReader(File.OpenRead(configPath)))
        {
            var toml = TOML.Parse(reader);
            var config = new ProjectConfig
            {
                Project = new ProjectSection
                {
                    Name = toml["project"]["name"],
                    Type = toml["project"]["type"],
                    InstallHeaders = toml["project"]["install_headers"]
                },
                Dependencies = new Dictionary<string, Dependency>()
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
                config.Resources.Files = filesArray.RawArray.Select(node => node.ToString()).Where(s => s != null).Select(s => s!).ToList();
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
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[bold red]Error reading {ConfigFileName}:[/] {ex.Message}");
        return null;
      }
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
    public static string? FindProjectRoot()
    {
      var currentDir = Directory.GetCurrentDirectory();
      while (currentDir != null)
      {
        if (File.Exists(Path.Combine(currentDir, ConfigFileName)))
        {
          return currentDir;
        }
        currentDir = Directory.GetParent(currentDir)?.FullName;
      }
      return null;
    }

    /// <summary>
    /// Saves the project configuration to package.toml file.
    /// </summary>
    /// <param name="config">The <see cref="ProjectConfig"/> object to serialize to TOML format.</param>
    /// <param name="project_name">
    /// Optional project name or path. If empty, uses the project root found via FindProjectRoot().
    /// </param>
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
    public static void SaveConfig(ProjectConfig config, string project_name = "")
    {
      try
      {
        var path = string.IsNullOrEmpty(project_name) ? FindProjectRoot() : project_name;
        if (path == null)
        {
          AnsiConsole.MarkupLine($"[bold red]Error writing to {PackageConfigFileName}:[/] Could not find project root");
          return;
        }

        var toml = new TomlTable
        {
            ["project"] = new TomlTable
            {
                ["name"] = config.Project.Name,
                ["type"] = config.Project.Type,
                ["install_headers"] = config.Project.InstallHeaders
            }
        };

        if (config.Dependencies.Any())
        {
            var depsTable = new TomlTable();
            foreach (var dep in config.Dependencies)
            {
                var depTable = new TomlTable
                {
                    IsInline = true,
                    ["git"] = dep.Value.Git,
                    ["tag"] = dep.Value.Tag
                };
                if (!string.IsNullOrEmpty(dep.Value.Target))
                {
                    depTable["target"] = dep.Value.Target;
                }
                depsTable[dep.Key] = depTable;
            }
            toml["dependencies"] = depsTable;
        }

        if (config.Resources.Files.Any())
        {
            var resourcesTable = new TomlTable();
            var filesArray = new TomlArray();
            foreach (var file in config.Resources.Files)
            {
                filesArray.Add(file);
            }
            resourcesTable["files"] = filesArray;
            toml["resources"] = resourcesTable;
        }
        
        if (config.Scripts.Any())
        {
            var scriptsTable = new TomlTable();
            foreach (var script in config.Scripts)
            {
                scriptsTable[script.Key] = script.Value;
            }
            toml["scripts"] = scriptsTable;
        }

        using (var writer = new StringWriter())
        {
            toml.WriteTo(writer);
            File.WriteAllText(Path.Combine(path, PackageConfigFileName), writer.ToString());
        }
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[bold red]Error writing to {PackageConfigFileName}:[/] {ex.Message}");
        throw;
      }
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
    public static string? GetProjectName()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null) return null;
      var config = LoadConfig();
      return config?.Project?.Name;
    }
  }
}
