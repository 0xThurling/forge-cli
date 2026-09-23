using System.Diagnostics;
using System.Text;
using DotMake.CommandLine;
using forge.CMakeGeneration;
using forge.Commands.Lua;
using forge.ForgeEngine.CoreUtils;
using forge.Models;
using Spectre.Console;
using InstallCommand = forge.Commands.Conan.InstallCommand;

namespace forge.Commands
{
  /// <summary>
  /// Generates CMakeLists.txt files and builds the C++ project.
  /// </summary>
  /// <remarks>
  /// This is the primary build command that orchestrates the entire build process:
  /// 1. Loads project configuration from forge.lua
  /// 2. Runs pre-build script (if defined)
  /// 3. Installs Conan dependencies
  /// 4. Generates embedded resource files
  /// 5. Generates CMakeLists.txt with all configuration
  /// 6. Runs CMake configure and build
  /// 7. Creates compile_commands.json symlink for LSP support
  /// 8. Runs post-build script (if defined)
  /// </remarks>
  /// <example>
  /// <code>
  /// // Standard build
  /// forge build
  /// 
  /// // Verbose build with C++17
  /// forge build --verbose --standard 17
  /// </code>
  /// </example>
  [CliCommand(Name = "build", Description = "Generate CMakeLists and build the project.", Parent = typeof(RootCommand))]
  public class BuildCommand
  {
    /// <summary>
    /// Gets or sets whether to show verbose CMake output.
    /// </summary>
    /// <value>
    /// When true, displays full CMake configure and build output. When false, shows
    /// only status messages. Defaults to false.
    /// </value>
    [CliOption(Description = "Show verbose output from CMake.")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets the C++ standard version to use.
    /// </summary>
    /// <value>
    /// Valid values: "11", "14", "17", "20". Defaults to "20".
    /// </value>
    /// <summary>
    /// Number of parallel build jobs. Null uses the generator's default (all
    /// cores); 1 forces a serial build.
    /// </summary>
    [CliOption(Description = "Parallel build jobs (default: all cores)", Required = false)]
    public int? Jobs { get; set; }

    /// <summary>
    /// Overrides the project's C++ standard for this invocation. Null when the
    /// flag was not given, so the configured standard wins.
    /// </summary>
    [CliOption(Description = "C++ standard to use (e.g., 11, 14, 17, 20). Defaults to the configured standard.", Required = false)]
    public string? Standard { get; set; }

    /// <summary>Production preset (-O3 -DNDEBUG) regardless of forge.lua.</summary>
    [CliOption(Description = "Build with the production preset (-O3 -DNDEBUG) regardless of config.")]
    public bool Release { get; set; }

    /// <summary>Debug preset (-O0 -g) regardless of forge.lua.</summary>
    [CliOption(Description = "Build with the debug preset (-O0 -g) regardless of config.")]
    public bool Debug { get; set; }

    /// <summary>Extra presets for a quick build (comma-separated).</summary>
    [CliOption(Description = "Add build presets for a quick build (comma-separated), e.g. --preset simd,concurrency.", Required = false)]
    public string? Preset { get; set; }

    /// <summary>Build only this CMake target (default: all of them).</summary>
    [CliOption(Description = "Build only this CMake target", Required = false)]
    public string? Target { get; set; }

    /// <summary>Ignore presets declared in forge.lua (use only CLI presets).</summary>
    [CliOption(Description = "Ignore presets declared in forge.lua (use only CLI presets).")]
    public bool NoConfigPresets { get; set; }

    /// <summary>
    /// Executes the full build pipeline including CMake generation and compilation.
    /// </summary>
    /// <returns>
    /// 0 if the build completed successfully, non-zero if there was an error.
    /// </returns>
    public async Task<int> RunAsync()
    {
      var projectConfig = await ProjectConfigManager.LoadConfigAsync();

      if (projectConfig == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `forge.lua` not found or is missing project name.");
        return 1;
      }

      // Everything this build contributes lives in one context, so nothing a
      // previous build (or another project in a workspace) left behind leaks in.
      var context = new BuildContext { Config = projectConfig };

      if (projectConfig.Scripts.TryGetValue("pre-build", out _))
      {
        var runCommand = new RunCommand { ScriptName = "pre-build" };
        if (await runCommand.RunAsync() != 0)
        {
          AnsiConsole.MarkupLine("[bold red]Error:[/] Pre-build script failed.");
          return 1;
        }
      }

      // Needs to run synchronously; a failing script stops the build.
      if (!Task.Run(() => LuaBuilder.RunBuilderScripts(context)).GetAwaiter().GetResult())
        return 1;

      var installPackages = new InstallCommand();
      if (await installPackages.InstallForBuildAsync(context) != 0)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Conan dependencies could not be installed.");
        return 1;
      }

      // Auto-create tests if testing is enabled
      if (projectConfig.Testing)
      {
        // Ensure test directory and googletest exist
        if (!Directory.Exists("test"))
        {
          await Utils.CreateTests();
        }

        // Refresh config to get the googletest dependency
        projectConfig = await ProjectConfigManager.LoadConfigAsync();
        if (projectConfig != null)
          context.Config = projectConfig;
      }

      // Apply CLI flag overrides (quick builds without editing forge.lua)
      if (projectConfig != null)
      {
        var build = projectConfig.Build;
        if (!string.IsNullOrWhiteSpace(Standard))
          projectConfig.Project.Standard = Standard;
        if (NoConfigPresets)
          build.Presets.Clear();
        if (Release)
          build.Presets.Add("production");
        if (Debug)
          build.Presets.Add("debug");
        if (!string.IsNullOrWhiteSpace(Preset))
          build.Presets.AddRange(
            Preset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
      }

      // Apply CLI flag overrides (quick builds without editing forge.lua)
      if (projectConfig != null)
      {
        var build = projectConfig.Build;
        if (NoConfigPresets)
          build.Presets.Clear();
        if (Release)
          build.Presets.Add("production");
        if (Debug)
          build.Presets.Add("debug");
        if (!string.IsNullOrWhiteSpace(Preset))
          build.Presets.AddRange(
            Preset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
      }

      if (projectConfig?.Project.Type == "library" && projectConfig.Project.InstallHeaders)
      {
        CoreUtils.GenerateLibraryHeaders(projectConfig.Project.Name);
      }

      var buildExitCode = AnsiConsole.Status().AutoRefresh(!Verbose).Start("Building Project...", _ =>
      {
        var projectName = projectConfig?.Project.Name;

        // Auto-detect CMake version and apply policy if needed
        var cmakeVersion = GetCmakeVersion();
        string? policyVersion = null;

        if (!string.IsNullOrEmpty(projectConfig?.Project.CmakePolicyVersion))
        {
          policyVersion = projectConfig.Project.CmakePolicyVersion;
        }
        else if (cmakeVersion.Major >= 4)
        {
          policyVersion = "3.5";
        }

        try
        {

          Directory.CreateDirectory(Path.Combine(".config", "cmake"));

          // Generate resource files if any
          if (projectConfig?.Resources.Files.Count != 0)
          {
            Utils.GenerateResourceFiles(projectConfig!.Resources.Files);
          }

          if (projectConfig.VcpkgDependencies.Count > 0)
            VcpkgManager.WriteManifest(projectConfig);

          var cmakeContent = CMakeRegistry.Instance.Generate(context);

          var cmakeConfigPath = Path.Combine(".config", "cmake", "CMakeLists.txt");
          File.WriteAllText(cmakeConfigPath, cmakeContent);

          var rootCmakeContent = new StringBuilder();

          rootCmakeContent.AppendLine($"cmake_minimum_required(VERSION 3.23)");

          if (!string.IsNullOrEmpty(policyVersion))
          {
            rootCmakeContent.AppendLine($"set(CMAKE_POLICY_VERSION_MINIMUM {policyVersion})");
          }

          rootCmakeContent.AppendLine();
          var versionArgument = string.IsNullOrWhiteSpace(projectConfig!.Project.Version)
            ? string.Empty
            : $" VERSION {projectConfig.Project.Version}";
          rootCmakeContent.AppendLine($"project({projectName}{versionArgument} LANGUAGES CXX C)");
          rootCmakeContent.AppendLine();
          rootCmakeContent.AppendLine("include(.config/cmake/CMakeLists.txt)");

          File.WriteAllText("CMakeLists.txt", rootCmakeContent.ToString());

          // Configure step
          EnsureBuildCacheMatchesProject("build");
          var buildType = Debug && !Release ? "Debug" : "Release";
          var build = projectConfig!.Build;
          var generator = build.Generator;

          // CMake can only scan for modules with Ninja or a recent Visual
          // Studio generator, so pick a working one unless the user insisted.
          if (build.Modules)
          {
            if (generator.Length == 0)
            {
              generator = "Ninja";
              AnsiConsole.MarkupLine("[dim]`modules = true` needs a scanning generator: using Ninja.[/]");
            }
            else if (!IsModuleCapableGenerator(generator))
            {
              AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] `modules = true` cannot work with `{generator}` — " +
                "module scanning needs Ninja or Visual Studio 17.4+.");
            }
          }

          var multiConfig = IsMultiConfigGenerator(generator);
          var jobCount = Jobs is > 0 ? Jobs.Value : build.Jobs;

          var cacheVariables = new Dictionary<string, string>(StringComparer.Ordinal)
          {
            ["CMAKE_EXPORT_COMPILE_COMMANDS"] = "ON",
            ["CMAKE_INSTALL_PREFIX"] = "."
          };

          // Multi-config generators choose the configuration at build time.
          if (!multiConfig)
            cacheVariables["CMAKE_BUILD_TYPE"] = buildType;

          if (!string.IsNullOrEmpty(policyVersion))
            cacheVariables["CMAKE_POLICY_VERSION_MINIMUM"] = policyVersion;

          if (build.CxxCompiler.Length > 0)
            cacheVariables["CMAKE_CXX_COMPILER"] = build.CxxCompiler;
          if (build.CCompiler.Length > 0)
            cacheVariables["CMAKE_C_COMPILER"] = build.CCompiler;
          if (build.CmakePrefixPath.Count > 0)
            cacheVariables["CMAKE_PREFIX_PATH"] = string.Join(";", build.CmakePrefixPath);

          // vcpkg's toolchain reads these, so they must be cache variables (set
          // before the toolchain runs), not plain `set()` calls.
          if (!string.IsNullOrWhiteSpace(projectConfig!.VcpkgTriplet))
            cacheVariables["VCPKG_TARGET_TRIPLET"] = projectConfig.VcpkgTriplet;
          if (build.SystemName.Length > 0)
            cacheVariables["CMAKE_SYSTEM_NAME"] = build.SystemName;
          if (build.SystemProcessor.Length > 0)
            cacheVariables["CMAKE_SYSTEM_PROCESSOR"] = build.SystemProcessor;

          var conanToolchain = Path.Combine("build", "build", buildType, "generators", "conan_toolchain.cmake");

          // CMAKE_TOOLCHAIN_FILE has one slot: an explicit toolchain wins, then
          // Conan's or vcpkg's, and combining them is an error.
          string? toolchainFile = null;
          if (build.ToolchainFile.Length > 0)
          {
            if (projectConfig.VcpkgDependencies.Count > 0 || File.Exists(conanToolchain))
            {
              AnsiConsole.MarkupLine(
                "[bold red]Error:[/] `toolchain_file` cannot be combined with Conan or vcpkg dependencies; they all set CMAKE_TOOLCHAIN_FILE.");
              return 1;
            }
            toolchainFile = build.ToolchainFile;
          }
          else if (projectConfig.VcpkgDependencies.Count > 0)
          {
            if (File.Exists(conanToolchain))
            {
              AnsiConsole.MarkupLine(
                "[bold red]Error:[/] Conan and vcpkg both need CMAKE_TOOLCHAIN_FILE; use one package manager per project.");
              return 1;
            }

            if (!VcpkgManager.Validate(projectConfig))
              return 1;

            toolchainFile = VcpkgManager.ToolchainFile(projectConfig);
          }
          else if (File.Exists(conanToolchain))
          {
            toolchainFile = conanToolchain;
          }

          if (toolchainFile is not null)
            cacheVariables["CMAKE_TOOLCHAIN_FILE"] = toolchainFile;

          // ArgumentList (not a command-line string) so values with spaces —
          // a generator like "Unix Makefiles", a path with spaces — survive.
          var configureArguments = new List<string> { "-B", "build", "-S", "." };
          if (generator.Length > 0)
          {
            configureArguments.Add("-G");
            configureArguments.Add(generator);
          }
          foreach (var (key, value) in cacheVariables)
            configureArguments.Add($"-D{key}={value}");

          // Mirror the same settings for IDEs and `cmake --preset forge`.
          CMakePresetsManager.Write(generator, cacheVariables);

          var cmakeConfigureCommand = new ProcessStartInfo("cmake")
          {
            RedirectStandardOutput = !Verbose,
            RedirectStandardError = !Verbose,
            UseShellExecute = false,
            CreateNoWindow = true,
          };
          foreach (var argument in configureArguments)
            cmakeConfigureCommand.ArgumentList.Add(argument);

          using (var process = Process.Start(cmakeConfigureCommand))
          {
            if (process == null) throw new Exception("Failed to start CMake process.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
              AnsiConsole.MarkupLine("[bold red]CMake configure failed.[/]");
              if (!Verbose)
              {
                Console.Write(process.StandardOutput.ReadToEnd());
                Console.Write(process.StandardError.ReadToEnd());
              }
              return 1;
            }
          }

          // Create a symlink in the root for the LSP
          var compileCommandsPath = Path.Combine("build", "compile_commands.json");
          var symlinkPath = "compile_commands.json";
          if (File.Exists(compileCommandsPath))
          {
            if (File.Exists(symlinkPath) || Directory.Exists(symlinkPath))
            {
              File.Delete(symlinkPath);
            }
            File.CreateSymbolicLink(symlinkPath, compileCommandsPath);
            if (Verbose)
            {
              AnsiConsole.MarkupLine("[bold green]--- Created compile_commands.json for LSP --- [/]");
            }
          }

          // Build step
          // Always build in parallel: without --parallel, CMake uses the
          // generator's default (serial for Makefiles).
          var buildArguments = new List<string> { "--build", "build", "--parallel" };
          if (jobCount > 0)
            buildArguments.Add(jobCount.ToString());
          if (!string.IsNullOrWhiteSpace(Target))
          {
            buildArguments.Add("--target");
            buildArguments.Add(Target);
          }
          if (multiConfig)
          {
            buildArguments.Add("--config");
            buildArguments.Add(buildType);
          }
          if (Verbose)
            buildArguments.Add("--verbose");

          var cmakeBuildCommand = new ProcessStartInfo("cmake")
          {
            RedirectStandardOutput = !Verbose,
            RedirectStandardError = !Verbose,
            UseShellExecute = false,
            CreateNoWindow = true,
          };
          foreach (var argument in buildArguments)
            cmakeBuildCommand.ArgumentList.Add(argument);

          using (var process = Process.Start(cmakeBuildCommand))
          {
            if (process == null) throw new Exception("Failed to start CMake process.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
              AnsiConsole.MarkupLine("[bold red]CMake build failed.[/]");
              if (!Verbose)
              {
                Console.Write(process.StandardOutput.ReadToEnd());
                Console.Write(process.StandardError.ReadToEnd());
              }
              return 1;
            }
          }

          if (projectConfig.Project.Type == "library")
          {
            return HandleLibraryBuild(projectConfig);
          }

          AnsiConsole.MarkupLine("[bold green]Build finished successfully.[/]");
          return 0;
        }
        catch (FileNotFoundException)
        {
          AnsiConsole.MarkupLine("[bold red]Error:[/] `cmake` command not found. Please ensure CMake is installed and in your PATH.");
          return 1;
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
          return 1;
        }
      });

      if (buildExitCode != 0)
        return buildExitCode;

      if (projectConfig!.Scripts.TryGetValue("post-build", out _))
      {
        var runCommand = new RunCommand { ScriptName = "post-build" };
        if (await runCommand.RunAsync() != 0)
        {
          AnsiConsole.MarkupLine("[bold red]Error:[/] Post-build script failed.");
          return 1;
        }
      }

      return 0;
    }

    private static int HandleLibraryBuild(ProjectConfig config)
    {
      var possiblePaths = new[]
      {
        "build/lib" + config.Project.Name + ".a",
        "build/" + config.Project.Name + ".lib",
        "build/lib" + config.Project.Name + ".so",
        "build/" + config.Project.Name + ".dll"
      };

      string? foundPath = null;
      foreach (var path in possiblePaths)
      {
        if (File.Exists(path))
        {
          foundPath = path;
          break;
        }
      }

      if (foundPath != null)
      {
        var fileInfo = new FileInfo(foundPath);
        AnsiConsole.MarkupLine($"[green]Library built successfully![/]");
        AnsiConsole.MarkupLine($"   Path: {foundPath}");
        AnsiConsole.MarkupLine($"   Size: {fileInfo.Length / 1024.0:F2} KB");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Note:[/] Libraries cannot be executed directly.");
        AnsiConsole.MarkupLine($"[dim]To use this library, add it as a dependency in another project or include headers from src/[/]");
      }
      else
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Library output not found in build/ directory.");
        AnsiConsole.MarkupLine($"[dim]Expected: {string.Join(", ", possiblePaths)}[/]");
      }

      return 0;
    }

    /// <summary>
    /// True for generators that select the configuration at build time
    /// (Visual Studio, Xcode, Ninja Multi-Config): they take <c>--config</c>
    /// instead of <c>CMAKE_BUILD_TYPE</c>.
    /// </summary>
    private static bool IsModuleCapableGenerator(string generator) =>
      generator.Contains("Ninja", StringComparison.OrdinalIgnoreCase) ||
      generator.Contains("Visual Studio 17", StringComparison.OrdinalIgnoreCase);

    private static bool IsMultiConfigGenerator(string generator) =>
      generator.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
      generator.Contains("Multi-Config", StringComparison.OrdinalIgnoreCase) ||
      generator.StartsWith("Xcode", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CMake refuses to configure when <c>build/CMakeCache.txt</c> was created
    /// for a different source directory — a moved, renamed or re-cloned
    /// checkout — with "The current CMakeCache.txt directory ... is different".
    /// Detect that here and drop the stale cache plus its generated files,
    /// keeping <c>_deps/*-src</c> so fetched dependency sources are not
    /// re-downloaded, then let the configure step start clean.
    /// </summary>
    private static void EnsureBuildCacheMatchesProject(string buildDir)
    {
      var cachePath = Path.Combine(buildDir, "CMakeCache.txt");
      if (!File.Exists(cachePath))
        return;

      string? cachedSource = null;
      foreach (var line in File.ReadLines(cachePath))
      {
        if (!line.StartsWith("CMAKE_HOME_DIRECTORY:", StringComparison.Ordinal))
          continue;

        var separator = line.IndexOf('=');
        if (separator >= 0)
          cachedSource = line[(separator + 1)..].Trim();
        break;
      }

      if (string.IsNullOrEmpty(cachedSource))
        return;

      static string Normalise(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

      var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

      if (string.Equals(Normalise(cachedSource),
                        Normalise(Directory.GetCurrentDirectory()), comparison))
        return;

      AnsiConsole.MarkupLine(
        $"[yellow]Note:[/] build/ was configured for [dim]{cachedSource}[/]; " +
        "regenerating the cache (fetched dependency sources are kept).");

      File.Delete(cachePath);

      var cmakeFiles = Path.Combine(buildDir, "CMakeFiles");
      if (Directory.Exists(cmakeFiles))
        Directory.Delete(cmakeFiles, true);

      // FetchContent's per-dependency subbuilds carry the same stale paths.
      var depsDir = Path.Combine(buildDir, "_deps");
      if (!Directory.Exists(depsDir))
        return;

      foreach (var dir in Directory.GetDirectories(depsDir))
      {
        var leaf = Path.GetFileName(dir);
        if (leaf.EndsWith("-build", StringComparison.Ordinal) ||
            leaf.EndsWith("-subbuild", StringComparison.Ordinal))
          Directory.Delete(dir, true);
      }
    }

    private static Version GetCmakeVersion()
    {
      try
      {
        var psi = new ProcessStartInfo("cmake", "--version")
        {
          RedirectStandardOutput = true,
          UseShellExecute = false,
          CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process == null) return new Version(0, 0);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // Output format: "cmake version 3.23.1"
        var line = output.Split('\n')[0];
        var versionStr = line.Split(' ')[^1];
        if (Version.TryParse(versionStr, out var version))
        {
          return version;
        }
      }
      catch { }
      return new Version(0, 0);
    }
  }
}
