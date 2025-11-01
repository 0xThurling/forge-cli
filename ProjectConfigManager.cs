using Tommy;
using Spectre.Console;
using cpm.Models;

namespace cpm
{
  public static class ProjectConfigManager
  {
    private const string PackageConfigFileName = "package.toml";

    public static ProjectConfig? LoadConfig()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null)
      {
        return null;
      }

      var configPath = Path.Combine(projectRoot, PackageConfigFileName);

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
        AnsiConsole.MarkupLine($"[bold red]Error reading {PackageConfigFileName}:[/] {ex.Message}");
        return null;
      }
    }

    public static string? FindProjectRoot()
    {
      var currentDir = Directory.GetCurrentDirectory();
      while (currentDir != null)
      {
        if (File.Exists(Path.Combine(currentDir, PackageConfigFileName)))
        {
          return currentDir;
        }
        currentDir = Directory.GetParent(currentDir)?.FullName;
      }
      return null;
    }

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

    public static string? GetProjectName()
    {
      var projectRoot = FindProjectRoot();
      if (projectRoot == null) return null;
      var config = LoadConfig();
      return config?.Project?.Name;
    }
  }
}
