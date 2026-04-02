using System.Diagnostics;
using System.Text;
using DotMake.CommandLine;
using forge.CMakeGeneration;
using forge.Commands.Lua;
using Spectre.Console;
using InstallCommand = forge.Commands.Conan.InstallCommand;

namespace forge.Commands
{
  /// <summary>
  /// Generates CMakeLists.txt files and builds the C++ project.
  /// </summary>
  /// <remarks>
  /// This is the primary build command that orchestrates the entire build process:
  /// 1. Loads project configuration from package.toml
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
    [CliOption(Description = "C++ standard to use (e.g., 11, 14, 17, 20). Defaults to 20.")]
    public string Standard { get; set; } = "20";

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

      if (projectConfig.Scripts.TryGetValue("pre-build", out _))
      {
        var runCommand = new RunCommand { ScriptName = "pre-build" };
        if (await runCommand.RunAsync() != 0)
        {
          AnsiConsole.MarkupLine("[bold red]Error:[/] Pre-build script failed.");
          return 1;
        }
      }

      var installPackages = new InstallCommand();
      if (await installPackages.RunAsync() != 0)
      {
        AnsiConsole.WriteLine("Error install conan packages");
        return 1;
      }

      if (projectConfig.Testing)
      {
        // Ensure test directory exists
        Directory.CreateDirectory("test");

        // Create basic test/main.cpp if it doesn't exist
        var testMainPath = Path.Combine("test", "main.cpp");
        if (!File.Exists(testMainPath))
        {
          var testMainContent = """
            #include <gtest/gtest.h>
            // Sample test - replace with your own tests
            TEST(HelloTest, BasicAssertions) {
              EXPECT_EQ(1, 1);
              EXPECT_TRUE(true);
            }
            int main(int argc, char **argv) {
              ::testing::InitGoogleTest(&argc, argv);
              return RUN_ALL_TESTS();
            }
            """;
          File.WriteAllText(testMainPath, testMainContent);
        }

        // Auto-add googletest if not present
        if (!projectConfig.Dependencies.ContainsKey("googletest"))
        {
          projectConfig.ConanDependencies["gtest"] = "1.14";
          AnsiConsole.MarkupLine("[bold green]Auto-added googletest dependency for testing.[/]");

          // Save the updated config
          ProjectConfigManager.SaveConfig(projectConfig);
        }
      }

      AnsiConsole.Status().AutoRefresh(!Verbose).Start("Building Project...", _ =>
      {
        var projectName = projectConfig.Project.Name;

        try
        {

          Directory.CreateDirectory(Path.Combine(".config", "cmake"));

          // Generate resource files if any
          if (projectConfig.Resources.Files.Count != 0)
          {
            Utils.GenerateResourceFiles(projectConfig.Resources.Files);
          }

          // Needs to run synchronously
          Task.Run(() => LuaBuilder.RunBuilderScripts()).Wait();

          var cmakeContent = CMakeRegistry.Instance.Generate(projectConfig, Standard);

          ProjectBuildManager.CustomCmakeSnippets.Clear();

          var cmakeConfigPath = Path.Combine(".config", "cmake", "CMakeLists.txt");
          File.WriteAllText(cmakeConfigPath, cmakeContent);

          var rootCmakeContent = new StringBuilder();

          rootCmakeContent.AppendLine($"cmake_minimum_required(VERSION 3.23)");
          rootCmakeContent.AppendLine();
          rootCmakeContent.AppendLine($"project({projectName} LANGUAGES CXX C)");
          rootCmakeContent.AppendLine();
          rootCmakeContent.AppendLine("include(.config/cmake/CMakeLists.txt)");

          File.WriteAllText("CMakeLists.txt", rootCmakeContent.ToString());

          // Configure step
          var cmakeArgs = new StringBuilder("-B build -DCMAKE_BUILD_TYPE=Release -S . -DCMAKE_EXPORT_COMPILE_COMMANDS=ON -DCMAKE_INSTALL_PREFIX=.");
          var toolchain = Path.Combine("build", "build", "Release", "generators", "conan_toolchain.cmake");

          if (File.Exists(toolchain))
          {
            cmakeArgs.Append($" -DCMAKE_TOOLCHAIN_FILE=\"{toolchain}\"");
          }

          var cmakeConfigureCommand = new ProcessStartInfo("cmake", cmakeArgs.ToString())
          {
            RedirectStandardOutput = !Verbose,
            RedirectStandardError = !Verbose,
            UseShellExecute = false,
            CreateNoWindow = true,
          };

          using (var process = Process.Start(cmakeConfigureCommand))
          {
            if (process == null) throw new Exception("Failed to start CMake process.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
              AnsiConsole.MarkupLine("[bold red]CMake configure failed.[/]");
              if (!Verbose)
              {
                AnsiConsole.Write(process.StandardOutput.ReadToEnd());
                AnsiConsole.Write(process.StandardError.ReadToEnd());
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
          var buildCommandArgs = new StringBuilder("--build build");
          if (Verbose)
          {
            buildCommandArgs.Append(" --verbose");
          }

          var cmakeBuildCommand = new ProcessStartInfo("cmake", buildCommandArgs.ToString())
          {
            RedirectStandardOutput = !Verbose,
            RedirectStandardError = !Verbose,
            UseShellExecute = false,
            CreateNoWindow = true,
          };

          using (var process = Process.Start(cmakeBuildCommand))
          {
            if (process == null) throw new Exception("Failed to start CMake process.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
              AnsiConsole.MarkupLine("[bold red]CMake build failed.[/]");
              if (!Verbose)
              {
                AnsiConsole.Write(process.StandardOutput.ReadToEnd());
                AnsiConsole.Write(process.StandardError.ReadToEnd());
              }
              return 1;
            }
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

      if (projectConfig.Scripts.TryGetValue("post-build", out _))
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
  }
}
