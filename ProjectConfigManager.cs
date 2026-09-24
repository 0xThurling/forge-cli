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

      // Work from the project root: the commands below use paths relative to it
      // (build/, .config/, src/, forge.lua), and the root was found by walking
      // upwards from the current directory.
      var root = Path.GetFullPath(projectRoot);
      if (root != Path.GetFullPath(Directory.GetCurrentDirectory()))
      {
        Directory.SetCurrentDirectory(root);
      }

      var configPath = Path.Combine(root, ConfigFileName);
      var config = await _luaLoader.LoadConfig(configPath);

      // A configuration without a project name cannot drive anything: treat it
      // as "not a forge project" instead of letting CMake fail later on an
      // empty project() line.
      if (config is null || string.IsNullOrWhiteSpace(config.Project.Name))
        return null;

      // Hot reload brings its engine in as a normal dependency, so every
      // command that only reads the config (`forge install` above all) sees it.
      if (config.Build.Hot)
        HotReload.EnsureDependency(config);

      // Resolve the version from Git once, here, so every consumer of the config
      // (project(), SOVERSION, CPack, vcpkg.json) agrees on it.
      if (config.Project.VersionFromGit)
        ResolveVersionFromGit(config, root);

      return config;
    }

    /// <summary>
    /// Replaces <see cref="ProjectSection.Version"/> with the version derived
    /// from the newest Git tag: <c>1.4.2</c> on a tag, <c>1.4.2.7</c> seven
    /// commits later. The declared version is kept when Git has nothing to say
    /// (no repository, no version-like tag).
    /// </summary>
    private static void ResolveVersionFromGit(ProjectConfig config, string projectRoot)
    {
      var (version, commits) = ForgeEngine.CoreUtils.GitInfo.DescribeVersion(projectRoot);
      if (string.IsNullOrWhiteSpace(version))
      {
        AnsiConsole.MarkupLine(
          "[yellow]Warning:[/] `version_from_git` is set but no version-like tag (v1.2.3) was found" +
          (string.IsNullOrWhiteSpace(config.Project.Version)
            ? "."
            : $"; keeping version = \"{config.Project.Version}\"."));
        return;
      }

      config.Project.Version = commits > 0 ? $"{version}.{commits}" : version;
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
            CmakePolicyVersion = toml["project"].HasKey("cmake_policy_version") ? toml["project"]["cmake_policy_version"] : "",
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
    /// Writes the project configuration back to <c>forge.lua</c>.
    /// </summary>
    /// <param name="config">The <see cref="ProjectConfig"/> to serialize.</param>
    /// <remarks>
    /// Used by the commands that change the configuration in place (<c>forge embed</c>,
    /// <c>forge.config.set</c>). Sections with data are written; empty ones are omitted.
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
      if (config.Project.VersionFromGit)
        sb.AppendLine("        version_from_git = true,");
      if (!string.IsNullOrWhiteSpace(config.Project.Description))
        sb.AppendLine($"        description = \"{config.Project.Description.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
      if (config.Project.PackageDepends.Count > 0 ||
          config.Project.DebDepends.Count > 0 ||
          config.Project.RpmDepends.Count > 0)
      {
        static string List(IEnumerable<string> values) =>
          "{ " + string.Join(", ", values.Select(value => $"\"{value}\"")) + " }";

        if (config.Project.DebDepends.Count == 0 && config.Project.RpmDepends.Count == 0)
        {
          sb.AppendLine($"        package_depends = {List(config.Project.PackageDepends)},");
        }
        else
        {
          sb.AppendLine("        package_depends = {");
          if (config.Project.PackageDepends.Count > 0)
            sb.AppendLine($"            {string.Join(", ", config.Project.PackageDepends.Select(v => $"\"{v}\""))},");
          if (config.Project.DebDepends.Count > 0)
            sb.AppendLine($"            deb = {List(config.Project.DebDepends)},");
          if (config.Project.RpmDepends.Count > 0)
            sb.AppendLine($"            rpm = {List(config.Project.RpmDepends)},");
          sb.AppendLine("        },");
        }
      }

      if (!string.IsNullOrWhiteSpace(config.Project.Contact))
        sb.AppendLine($"        contact = \"{config.Project.Contact.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
      if (!string.IsNullOrEmpty(config.Project.Linkage) &&
          !config.Project.Linkage.Equals("static", StringComparison.OrdinalIgnoreCase))
      {
        sb.AppendLine($"        linkage = \"{config.Project.Linkage}\",");
      }
      if (!string.IsNullOrEmpty(config.Project.Version))
      {
        sb.AppendLine($"        version = \"{config.Project.Version}\",");
      }
      if (!string.IsNullOrEmpty(config.Project.CmakePolicyVersion))
      {
        sb.AppendLine($"        cmake_policy_version = \"{config.Project.CmakePolicyVersion}\",");
      }
      if (config.Project.InstallHeaders)
      {
        sb.AppendLine("        install_headers = true,");
      }
      sb.AppendLine("    },");
      // Dependencies section - always output structure
      if (config.Targets.Count > 0)
      {
        sb.AppendLine("    targets = {");
        foreach (var target in config.Targets)
        {
          var sources = target.Sources.Count > 0
            ? "{ " + string.Join(", ", target.Sources.Select(source => $"\"{source}\"")) + " }"
            : "{}";
          sb.AppendLine("        {");
          sb.AppendLine($"            name = \"{target.Name}\",");
          sb.AppendLine($"            type = \"{target.Type}\",");
          if (target.Type == "library" && target.Linkage != "static")
            sb.AppendLine($"            linkage = \"{target.Linkage}\",");
          sb.AppendLine($"            sources = {sources},");
          if (target.Install.HasValue)
            sb.AppendLine($"            install = {target.Install.Value.ToString().ToLowerInvariant()},");
          sb.AppendLine("        },");
        }
        sb.AppendLine("    },");
      }

      if (config.TestFramework != "gtest" || config.Benchmark)
      {
        sb.AppendLine("    testing = {");
        sb.AppendLine($"        enabled = {config.Testing.ToString().ToLower()},");
        sb.AppendLine($"        framework = \"{config.TestFramework}\",");
        sb.AppendLine($"        benchmark = {config.Benchmark.ToString().ToLower()},");
        sb.AppendLine("    },");
      }
      else
      {
        sb.AppendLine($"    testing = {config.Testing.ToString().ToLower()},");
      }
      // Dependencies section - always output structure
      sb.AppendLine("    dependencies = {");

      if (config.Dependencies.Count > 0)
      {
        sb.AppendLine("        direct = {");
        foreach (var dep in config.Dependencies)
        {
          sb.AppendLine($"            {dep.Key} = {{");
          // A dependency is either a local checkout or a git fetch; writing
          // both would be ambiguous, and dropping `path` would silently turn a
          // local dependency back into a fetch.
          if (!string.IsNullOrEmpty(dep.Value.Path))
          {
            sb.AppendLine($"                path = \"{dep.Value.Path}\",");
          }
          else
          {
            if (!string.IsNullOrEmpty(dep.Value.Git))
              sb.AppendLine($"                git = \"{dep.Value.Git}\",");
            if (!string.IsNullOrEmpty(dep.Value.Tag))
              sb.AppendLine($"                tag = \"{dep.Value.Tag}\",");
          }
          if (!string.IsNullOrEmpty(dep.Value.Target))
            sb.AppendLine($"                target = \"{dep.Value.Target}\",");
          if (dep.Value.IsExportable)
          {
            sb.AppendLine("                export = {");
            sb.AppendLine($"                    package = \"{dep.Value.ExportPackage}\",");
            sb.AppendLine($"                    target = \"{dep.Value.ExportTarget}\",");
            sb.AppendLine("                },");
          }
          if (dep.Value.Options.Count > 0)
          {
            sb.AppendLine("                options = {");
            foreach (var (optionKey, optionValue) in dep.Value.Options)
              sb.AppendLine($"                    {optionKey} = \"{optionValue}\",");
            sb.AppendLine("                },");
          }
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
      if (config.PkgConfigDependencies.Count > 0)
      {
        sb.AppendLine("        pkgconfig = {");
        foreach (var module in config.PkgConfigDependencies)
          sb.AppendLine($"            \"{module}\",");
        sb.AppendLine("        },");
      }
      if (config.VcpkgDependencies.Count > 0)
      {
        sb.AppendLine("        vcpkg = {");
        foreach (var (name, dependency) in config.VcpkgDependencies)
        {
          if (string.IsNullOrWhiteSpace(dependency.Version))
          {
            sb.AppendLine($"            {name} = \"{dependency.Target}\",");
          }
          else
          {
            sb.AppendLine($"            {name} = {{ target = \"{dependency.Target}\", version = \"{dependency.Version}\" }},");
          }
        }
        sb.AppendLine("        },");
      }
      sb.AppendLine("    },");
      // Build flags section - only output if there is anything to write
      if (config.Build.HasAny)
      {
        sb.AppendLine("    build = {");
        if (config.Build.Cache != "shared")
          sb.AppendLine($"        cache = \"{config.Build.Cache}\",");
        if (config.Build.Unity)
          sb.AppendLine("        unity = true,");
        if (config.Build.Pch.Length > 0)
          sb.AppendLine($"        pch = \"{config.Build.Pch}\",");
        if (config.Build.Modules)
          sb.AppendLine("        modules = true,");
        if (config.Build.Hot)
          sb.AppendLine("        hot = true,");
        if (config.Build.CxxCompiler.Length > 0)
          sb.AppendLine($"        cxx_compiler = \"{config.Build.CxxCompiler}\",");
        if (config.Build.CCompiler.Length > 0)
          sb.AppendLine($"        c_compiler = \"{config.Build.CCompiler}\",");
        if (config.Build.ToolchainFile.Length > 0)
          sb.AppendLine($"        toolchain_file = \"{config.Build.ToolchainFile}\",");
        if (config.Build.CmakePrefixPath.Count > 0)
          sb.AppendLine($"        cmake_prefix_path = {{ {string.Join(", ", config.Build.CmakePrefixPath.Select(p => $"\"{p}\""))} }},");
        if (config.Build.SystemName.Length > 0)
          sb.AppendLine($"        system_name = \"{config.Build.SystemName}\",");
        if (config.Build.SystemProcessor.Length > 0)
          sb.AppendLine($"        system_processor = \"{config.Build.SystemProcessor}\",");
        if (config.Build.Generator.Length > 0)
          sb.AppendLine($"        generator = \"{config.Build.Generator}\",");
        if (config.Build.Jobs > 0)
          sb.AppendLine($"        jobs = {config.Build.Jobs},");
        if (config.Build.CompilerLauncher.Length > 0)
          sb.AppendLine($"        compiler_launcher = \"{config.Build.CompilerLauncher}\",");
        if (config.Build.Presets.Count > 0)
          sb.AppendLine($"        presets = {{ {string.Join(", ", config.Build.Presets.Select(p => $"\"{p}\""))} }},");
        if (config.Build.CompileOptions.Count > 0)
          sb.AppendLine($"        compile_options = {{ {string.Join(", ", config.Build.CompileOptions.Select(o => $"\"{o}\""))} }},");
        if (config.Build.CompileDefinitions.Count > 0)
          sb.AppendLine($"        compile_definitions = {{ {string.Join(", ", config.Build.CompileDefinitions.Select(o => $"\"{o}\""))} }},");
        if (config.Build.LinkOptions.Count > 0)
          sb.AppendLine($"        link_options = {{ {string.Join(", ", config.Build.LinkOptions.Select(o => $"\"{o}\""))} }},");
        if (config.Build.LinkLibraries.Count > 0)
          sb.AppendLine($"        link_libraries = {{ {string.Join(", ", config.Build.LinkLibraries.Select(o => $"\"{o}\""))} }},");
        sb.AppendLine("    },");
      }
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
