using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge
{
  /// <summary>
  /// Provides utility functions for common operations used throughout Forge,
  /// including resource file generation and test setup.
  /// </summary>
  /// <remarks>
  /// This static class contains helper methods that don't belong to a specific
  /// domain but are needed by multiple commands. Functions include generating
  /// embedded resource files from binary assets and setting up Google Test.
  /// </remarks>
  public static class Utils
  {
    /// <summary>
    /// Generates C++ header and source files that embed binary resources directly
    /// into the compiled executable.
    /// </summary>
    /// <param name="resources">A list of file paths to resources that should be embedded.</param>
    /// <remarks>
    /// This method reads each specified binary file and generates C++ code that embeds
    /// the file contents as byte arrays. The generated code provides a simple API
    /// (Embedded::get()) to access the embedded resources at runtime.
    /// 
    /// The generated files are:
    /// - src/embedded_resources.h - Header with Embedded namespace declarations
    /// - src/embedded_resources.cpp - Implementation with byte arrays and lookup map
    /// </remarks>
    /// <example>
    /// <code>
    /// // Embed resources listed in package.toml
    /// var resources = new List<string> { "assets/icon.png", "assets/shader.glsl" };
    /// Utils.GenerateResourceFiles(resources);
    /// </code>
    /// </example>
    /// <summary>Writes a file only when its content differs.</summary>
    /// <returns>True when the file was (re)written.</returns>
    public static bool WriteIfChanged(string path, string content)
    {
      if (File.Exists(path) && File.ReadAllText(path) == content)
        return false;

      var directory = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
      File.WriteAllText(path, content);
      return true;
    }

    /// <summary>
    /// The configure inputs, serialised: a change here means CMake has to run
    /// again even when the generated files are byte-identical.
    /// </summary>
    public static string ConfigureStamp(
      string generator, Dictionary<string, string> cacheVariables, IEnumerable<string> inputs)
    {
      var sb = new StringBuilder();
      sb.Append("generator=").Append(generator).Append('\n');
      foreach (var (key, value) in cacheVariables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        sb.Append(key).Append('=').Append(value).Append('\n');
      foreach (var input in inputs)
        sb.Append(input).Append('\n');
      return sb.ToString();
    }

    /// <summary>
    /// A hash of the resource list and each file's size and write time: the
    /// signal for "the generated file would be identical".
    /// </summary>
    private static string ResourceStamp(List<string> resources)
    {
      var sb = new StringBuilder();
      foreach (var path in resources)
      {
        if (File.Exists(path))
        {
          var info = new FileInfo(path);
          sb.Append(path).Append('|').Append(info.Length).Append('|')
            .Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        else
        {
          sb.Append(path).Append("|missing\n");
        }
      }

      var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
      return Convert.ToHexString(hash);
    }

    public static void GenerateResourceFiles(List<string> resources)
    {
      var headerPath = Path.Combine("src", "embedded_resources.h");
      var cppPath = Path.Combine("src", "embedded_resources.cpp");
      var stampPath = Path.Combine("build", ".forge-resources.stamp");

      // The generated file embeds every resource's bytes, so rewriting it
      // recompiles that translation unit. Leave it alone when the inputs are
      // the same as last time.
      var stamp = ResourceStamp(resources);
      if (File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp &&
          File.Exists(headerPath) && File.Exists(cppPath))
        return;

      AnsiConsole.MarkupLine("[bold cyan]--- Generating resource files --- [/]");

      // --- Generate Header File ---
      var headerLines = new List<string>
            {
                "#ifndef EMBEDDED_RESOURCES_H",
                "#define EMBEDDED_RESOURCES_H",
                "",
                "#include <string>",
                "#include <cstddef>",
                "",
                "namespace Embedded {",
                "    struct Resource {",
                "        const unsigned char* data;",
                "        size_t size;",
                "    };",
                "",
                "    const Resource& get(const std::string& name);",
                "}",
                "",
                "#endif // EMBEDDED_RESOURCES_H"
            };
      File.WriteAllLines(headerPath, headerLines);

      // --- Generate Cpp File ---
      var cppLines = new List<string>
            {
                "#include \"embedded_resources.h\"",
                "#include <map>",
                "#include <string>",
                ""
            };

      var validResources = new List<string>();
      foreach (var resourcePath in resources)
      {
        if (!File.Exists(resourcePath))
        {
          AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] Resource file not found: {resourcePath}. Skipping.");
          continue;
        }
        validResources.Add(resourcePath);

        var varName = SanitizeFileName(resourcePath);
        cppLines.Add($"// Resource: {resourcePath}");

        var data = File.ReadAllBytes(resourcePath);

        cppLines.Add($"const unsigned char {varName}_data[] = {{");
        var line = new StringBuilder("    ");
        for (var i = 0; i < data.Length; i++)
        {
          line.Append($"0x{data[i]:x2}, ");
          if ((i + 1) % 16 == 0)
          {
            cppLines.Add(line.ToString());
            line.Clear().Append("    ");
          }
        }
        if (line.Length > 4)
        {
          cppLines.Add(line.ToString().TrimEnd(' ', ','));
        }
        cppLines.Add("};");
        cppLines.Add($"const size_t {varName}_size = {data.Length};");
        cppLines.Add("");
      }

      cppLines.AddRange([
          "namespace Embedded {",
                "    static const std::map<std::string, Resource> resource_map = {"
      ]);
      foreach (var resourcePath in validResources)
      {
        // Key by the path recorded in forge.lua (forward slashes). The bare
        // file name would not match what the project registered, and two files
        // with the same name in different directories would collide.
        var key = resourcePath.Replace('\\', '/');
        var varName = SanitizeFileName(resourcePath);
        cppLines.Add($"        {{\"{key}\", {{{varName}_data, {varName}_size}}}},");
      }
      cppLines.AddRange([
          "    };",
                "",
                "    const Resource& get(const std::string& name) {",
                "        return resource_map.at(name);",
                "    }",
                "}",
            ]);

      File.WriteAllLines(cppPath, cppLines);
      WriteIfChanged(stampPath, stamp);

      AnsiConsole.MarkupLine($"[bold green]Successfully generated `[bold]{headerPath}[/]` and `[bold]{cppPath}[/][/]`.");
    }

    /// <summary>
    /// Converts a filename into a valid C++ identifier by replacing non-alphanumeric
    /// characters with underscores.
    /// </summary>
    /// <param name="fileName">The original filename to sanitize.</param>
    /// <returns>A string that can be used as a valid C++ variable name.</returns>
    /// <remarks>
    /// This method is used when generating embedded resource files to create valid
    /// C++ variable names from file paths. For example, "assets/icon.png" becomes
    /// "assets_icon_png".
    /// </remarks>
    /// <example>
    /// <code>
    /// var safeName = Utils.SanitizeFileName("assets/my-image.png");
    /// // Returns: "assets_my_image_png"
    /// </code>
    /// </example>
    private static string SanitizeFileName(string fileName)
    {
      // Replace non-alphanumeric characters with underscores
      var sb = new StringBuilder();
      foreach (char c in fileName)
      {
        if (char.IsLetterOrDigit(c))
        {
          sb.Append(c);
        }
        else
        {
          sb.Append('_');
        }
      }
      return sb.ToString();
    }

    /// <summary>
    /// Creates the test directory structure and configures Google Test for the project.
    /// </summary>
    /// <remarks>
    /// This method sets up the testing infrastructure by:
    /// 1. Creating the test/ directory if it doesn't exist
    /// 2. Creating a basic test/main.cpp with sample tests
    /// 3. Adding googletest as a dependency in package.toml if not already present
    /// 
    /// This is called automatically by the test command when tests need to be set up.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize testing framework
    /// Utils.CreateTests();
    /// </code>
    /// </example>
    public static async Task CreateTests()
    {
      // Create tests directory
      Directory.CreateDirectory("test");

      // Create test/main.cpp
      var testMainCppContent = """
            #include <gtest/gtest.h>

            // Demonstrate some basic assertions.
            TEST(HelloTest, BasicAssertions) {
                // Expect two strings not to be equal.
                EXPECT_STRNE("hello", "world");
                // Expect equality.
                EXPECT_EQ(7 * 6, 42);
            }
            """;

      File.WriteAllText(Path.Combine("test", "main.cpp"), testMainCppContent);

      // Add googletest to package.toml
      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `forge.lua` not found.");
        return; // Or throw an exception
      }

      if (!config.Dependencies.ContainsKey("googletest"))
      {
        config.Testing = true;
        config.Dependencies.Add("googletest", new Dependency
        {
          Git = "https://github.com/google/googletest.git",
          Tag = "v1.14.0"
        });
        ProjectConfigManager.SaveConfig(config);
        AnsiConsole.MarkupLine("[bold green]Added googletest dependency to forge.lua.[/]");
      }
      else
      {
        AnsiConsole.MarkupLine("[yellow]googletest dependency already exists in forge.lua.[/]");
      }

      AnsiConsole.MarkupLine("[bold green]Tests created successfully.[/]");
    }
  }
}
