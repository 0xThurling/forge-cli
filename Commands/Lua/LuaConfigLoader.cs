using forge.Models;
using Lua;
using Lua.Standard;
using Spectre.Console;

namespace forge.Commands.Lua;

public class LuaConfigLoader
{
  private LuaState? _state;

  /// <summary>
  /// Warnings already printed in this process. The configuration is loaded more
  /// than once per command (the build reloads it, the install step loads it
  /// again), and repeating the same warning three times is noise.
  /// </summary>
  private static readonly HashSet<string> _warned = new(StringComparer.Ordinal);

  private static void WarnOnce(string message)
  {
    if (_warned.Add(message))
      AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] {message}");
  }

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

    // Parse extra build targets
    if (table["targets"].TryRead<LuaTable>(out var targetsTable))
    {
      config.Targets = ParseTargets(targetsTable, config.Project.Name);
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

    // Parse Build flags
    if (table["build"].TryRead<LuaTable>(out var buildTable))
    {
      ParseBuild(ref config, buildTable);
    }

    // `testing = true` or `testing = { framework = "...", benchmark = true }`
    var testingValue = table["testing"];
    if (testingValue.TryRead<LuaTable>(out var testingTable))
    {
      var enabled = testingTable["enabled"];
      config.Testing = enabled == LuaValue.Nil ||
                       (bool.TryParse(enabled.ToString(), out var te) && te);
      if (testingTable["framework"] != LuaValue.Nil)
        config.TestFramework = testingTable["framework"].ToString().ToLowerInvariant();
      if (testingTable["benchmark"] != LuaValue.Nil)
        config.Benchmark = bool.TryParse(testingTable["benchmark"].ToString(), out var tb) && tb;
    }
    else if (testingValue != LuaValue.Nil)
    {
      config.Testing = bool.TryParse(testingValue.ToString(), out var t) && t;
    }

    // vcpkg settings live next to the dependency table.
    if (table["vcpkg_root"] != LuaValue.Nil)
      config.VcpkgRoot = table["vcpkg_root"].ToString();
    if (table["vcpkg_baseline"] != LuaValue.Nil)
      config.VcpkgBaseline = table["vcpkg_baseline"].ToString();
    if (table["vcpkg_triplet"] != LuaValue.Nil)
      config.VcpkgTriplet = table["vcpkg_triplet"].ToString();

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
      if (key is "project" or "dependencies" or "resources" or "scripts" or "features" or "custom" or "build" or "testing")
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

        // Compare against Nil rather than TryRead<LuaValue>: reading a boolean
        // through TryRead fails, which silently left every feature disabled.
        var enabled = featureTable["enabled"];
        if (enabled != LuaValue.Nil)
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

  private static void ParseBuild(ref ProjectConfig config, LuaTable table)
  {
    if (table["cxx_compiler"] != LuaValue.Nil)
      config.Build.CxxCompiler = table["cxx_compiler"].ToString();
    if (table["c_compiler"] != LuaValue.Nil)
      config.Build.CCompiler = table["c_compiler"].ToString();
    if (table["toolchain_file"] != LuaValue.Nil)
      config.Build.ToolchainFile = table["toolchain_file"].ToString();
    if (table["cmake_prefix_path"] != LuaValue.Nil)
      config.Build.CmakePrefixPath = ReadStringList(table["cmake_prefix_path"]);
    if (table["system_name"] != LuaValue.Nil)
      config.Build.SystemName = table["system_name"].ToString();
    if (table["system_processor"] != LuaValue.Nil)
      config.Build.SystemProcessor = table["system_processor"].ToString();
    if (table["generator"] != LuaValue.Nil)
      config.Build.Generator = table["generator"].ToString();
    if (table["jobs"] != LuaValue.Nil && int.TryParse(table["jobs"].ToString(), out var jobs))
      config.Build.Jobs = jobs;

    if (table["cache"] != LuaValue.Nil)
      config.Build.Cache = table["cache"].ToString();

    if (table["unity"] != LuaValue.Nil)
      config.Build.Unity = bool.TryParse(table["unity"].ToString(), out var unity) && unity;
    if (table["pch"] != LuaValue.Nil)
      config.Build.Pch = table["pch"].ToString();
    if (table["modules"] != LuaValue.Nil)
      config.Build.Modules = bool.TryParse(table["modules"].ToString(), out var modules) && modules;
    if (table["hot"] != LuaValue.Nil)
      config.Build.Hot = bool.TryParse(table["hot"].ToString(), out var hot) && hot;

    var launcher = table["compiler_launcher"];
    if (launcher != LuaValue.Nil)
      config.Build.CompilerLauncher = launcher.ToString();

    config.Build.Presets = ReadStringList(table["presets"]);
    // Documented names first (docs/project-configuration.md), with the earlier
    // spellings accepted so existing projects keep working.
    config.Build.CompileOptions =
      ReadStringList(table["cxx_flags"], table["compile_options"]);
    config.Build.CompileDefinitions =
      ReadStringList(table["compile_definitions"], table["definitions"]);
    config.Build.LinkOptions =
      ReadStringList(table["link_flags"], table["link_options"]);
    config.Build.LinkLibraries = ReadStringList(table["link_libraries"]);
  }

  /// <summary>
  /// Reads one or more Lua values as a list of strings: a table becomes its
  /// entries, a scalar becomes a one-element list, and nil is skipped. Passing
  /// several values merges them, which is how a documented key and its earlier
  /// spelling are both accepted.
  /// </summary>
  private static List<string> ReadStringList(params LuaValue[] values)
  {
    var list = new List<string>();

    foreach (var value in values)
    {
      if (value.TryRead<LuaTable>(out var table))
      {
        foreach (var kvp in table)
        {
          var text = kvp.Value.ToString();
          if (!string.IsNullOrWhiteSpace(text)) list.Add(text);
        }
      }
      else if (value != LuaValue.Nil)
      {
        var text = value.ToString();
        if (!string.IsNullOrWhiteSpace(text)) list.Add(text);
      }
    }

    return list;
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
        if (!directTable[name].TryRead<LuaTable>(out var depTable))
          continue;

        // An extra nesting level is the most common shape mistake, and it used
        // to become a dependency literally named "direct" — which then failed
        // at link time as `-ldirect`.
        if (name is "direct" or "conan" or "vcpkg" or "pkgconfig")
        {
          WarnOnce(
            $"`dependencies.direct.{name}` looks like an extra nesting level — " +
            $"dependencies belong directly under `direct`. Ignoring it.");
          continue;
        }

        var dependency = ParseDependencyFromTable(depTable);

        // Export metadata is all-or-nothing: the package to find and the target
        // it provides are only useful together.
        if (depTable["export"] != LuaValue.Nil && !depTable["export"].TryRead<LuaTable>(out _))
        {
          WarnOnce(
            $"dependency `{name}`: `export` must be a table like " +
            "{ package = \"pkg\", target = \"pkg::pkg\" }; ignoring it.");
        }
        else if (!string.IsNullOrWhiteSpace(dependency.ExportPackage) !=
                 !string.IsNullOrWhiteSpace(dependency.ExportTarget))
        {
          WarnOnce(
            $"dependency `{name}`: `export` needs both `package` and `target`; ignoring the export metadata.");
          dependency.ExportPackage = string.Empty;
          dependency.ExportTarget = string.Empty;
        }

        // A dependency without a source cannot be fetched or linked; keeping it
        // would put an unresolvable target on the link line.
        if (string.IsNullOrWhiteSpace(dependency.Path) &&
            (string.IsNullOrWhiteSpace(dependency.Git) || string.IsNullOrWhiteSpace(dependency.Tag)))
        {
          WarnOnce(
            $"dependency `{name}` has no source — give it `git` with `tag`, or `path`. Ignoring it.");
          continue;
        }

        config.Dependencies[name] = dependency;
      }
    }

    if (table["pkgconfig"].TryRead<LuaTable>(out var pkgConfigTable))
    {
      config.PkgConfigDependencies = ReadStringList(pkgConfigTable);
    }

    if (table["vcpkg"].TryRead<LuaTable>(out var vcpkgTable))
    {
      foreach (var kvp in vcpkgTable)
      {
        var name = kvp.Key.ToString();
        var dep = new VcpkgDependency();

        if (kvp.Value.TryRead<LuaTable>(out var options))
        {
          if (options["target"] != LuaValue.Nil)
            dep.Target = options["target"].ToString();
          if (options["version"] != LuaValue.Nil)
            dep.Version = options["version"].ToString();
        }
        else if (kvp.Value != LuaValue.Nil)
        {
          // Shorthand: `fmt = "fmt::fmt"`.
          dep.Target = kvp.Value.ToString();
        }

        config.VcpkgDependencies[name] = dep;
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

  /// <summary>
  /// Reads the <c>targets</c> array. Entries without a usable name, or that
  /// duplicate the project or each other, are reported and skipped rather than
  /// producing a broken CMake file.
  /// </summary>
  private static List<ProjectTarget> ParseTargets(LuaTable table, string projectName)
  {
    var targets = new List<ProjectTarget>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { projectName };

    foreach (var entry in table)
    {
      if (!entry.Value.TryRead<LuaTable>(out var targetTable))
        continue;

      var name = targetTable["name"] != LuaValue.Nil ? targetTable["name"].ToString() : string.Empty;
      if (string.IsNullOrWhiteSpace(name) ||
          !name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
      {
        AnsiConsole.MarkupLine(
          $"[bold yellow]Warning:[/] skipping a target with an unusable name `{name}` " +
          "(letters, digits, `_` and `-` only).");
        continue;
      }

      if (!seen.Add(name))
      {
        AnsiConsole.MarkupLine(
          $"[bold yellow]Warning:[/] skipping target `{name}`: the name is already used.");
        continue;
      }

      var target = new ProjectTarget { Name = name };

      if (targetTable["type"] != LuaValue.Nil)
        target.Type = targetTable["type"].ToString().ToLowerInvariant();
      if (target.Type is not ("executable" or "library"))
      {
        AnsiConsole.MarkupLine(
          $"[bold yellow]Warning:[/] target `{name}`: unknown type `{target.Type}` — using `executable`.");
        target.Type = "executable";
      }

      if (targetTable["linkage"] != LuaValue.Nil)
        target.Linkage = targetTable["linkage"].ToString().ToLowerInvariant();

      if (targetTable["sources"].TryRead<LuaTable>(out var sourcesTable))
        target.Sources = ReadStringList(sourcesTable);
      else if (targetTable["sources"] != LuaValue.Nil)
        target.Sources = ReadStringList(targetTable["sources"]);

      // Default: a directory named after the target.
      if (target.Sources.Count == 0)
        target.Sources = [name];

      if (targetTable["install"] != LuaValue.Nil &&
          bool.TryParse(targetTable["install"].ToString(), out var install))
      {
        target.Install = install;
      }

      targets.Add(target);
    }

    return targets;
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

    if (table["path"] != LuaValue.Nil)
    {
      dep.Path = table["path"].ToString();
    }

    if (table["target"] != LuaValue.Nil)
    {
      dep.Target = table["target"].ToString();
    }

    if (table.TryGetValue("export", out var exportValue) &&
        exportValue.TryRead<LuaTable>(out var exportTable))
    {
      if (exportTable["package"] != LuaValue.Nil)
        dep.ExportPackage = exportTable["package"].ToString();
      if (exportTable["target"] != LuaValue.Nil)
        dep.ExportTarget = exportTable["target"].ToString();
    }

    if (table.TryGetValue("options", out var optionsValue) &&
        optionsValue.TryRead<LuaTable>(out var optionsTable))
    {
      foreach (var (key, value) in optionsTable)
      {
        var name = key.ToString();
        if (!string.IsNullOrWhiteSpace(name) && value != LuaValue.Nil)
          dep.Options[name] = value.ToString();
      }
    }

    return dep;
  }

  private static void ParseProjectSection(ref ProjectConfig config, LuaTable table)
  {
    if (table["name"] != LuaValue.Nil)
    {
      config.Project.Name = table["name"].ToString().ToLower();
    }
    if (table["type"] != LuaValue.Nil)
    {
      config.Project.Type = table["type"].ToString().ToLower();
    }
    if (table["standard"] != LuaValue.Nil)
    {
      config.Project.Standard = table["standard"].ToString().ToLower();
    }
    if (table["cmake_policy_version"] != LuaValue.Nil)
    {
      config.Project.CmakePolicyVersion = table["cmake_policy_version"].ToString();
    }
    if (table["linkage"] != LuaValue.Nil)
    {
      config.Project.Linkage = table["linkage"].ToString().ToLower();
    }

    if (table["version"] != LuaValue.Nil)
    {
      config.Project.Version = table["version"].ToString();
    }
    if (table["version_from_git"] != LuaValue.Nil)
    {
      config.Project.VersionFromGit = bool.TryParse(table["version_from_git"].ToString(), out var fromGit) && fromGit;
    }
    // `package_depends = { "a", "b" }` applies to both package formats, while
    // `package_depends = { deb = {…}, rpm = {…} }` is format-specific.
    if (table["package_depends"].TryRead<LuaTable>(out var dependsTable))
    {
      foreach (var entry in dependsTable)
      {
        if (entry.Key.TryRead<double>(out _))
        {
          var value = entry.Value.ToString();
          if (!string.IsNullOrWhiteSpace(value))
            config.Project.PackageDepends.Add(value);
          continue;
        }

        var format = entry.Key.ToString().ToLowerInvariant();
        var values = ReadStringList(entry.Value);
        if (format == "deb")
          config.Project.DebDepends.AddRange(values);
        else if (format == "rpm")
          config.Project.RpmDepends.AddRange(values);
      }
    }
    else if (table["package_depends"] != LuaValue.Nil)
    {
      config.Project.PackageDepends.AddRange(ReadStringList(table["package_depends"]));
    }

    if (table["description"] != LuaValue.Nil)
    {
      config.Project.Description = table["description"].ToString();
    }
    if (table["contact"] != LuaValue.Nil)
    {
      config.Project.Contact = table["contact"].ToString();
    }

    // Default install_headers to true for libraries, otherwise follow the lua value if provided
    if (table["install_headers"] != LuaValue.Nil)
    {
      config.Project.InstallHeaders = bool.TryParse(table["install_headers"].ToString(), out var v) && v;
    }
    else if (config.Project.Type == "library")
    {
      config.Project.InstallHeaders = true;
    }
  }
}
