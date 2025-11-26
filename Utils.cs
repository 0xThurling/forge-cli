using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge
{
    public static class Utils
    {
        public static void GenerateResourceFiles(List<string> resources)
        {
            AnsiConsole.MarkupLine("[bold cyan]--- Generating resource files --- [/]");

            var headerPath = Path.Combine("src", "embedded_resources.h");
            var cppPath = Path.Combine("src", "embedded_resources.cpp");

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

            cppLines.AddRange(new List<string>
            {
                "namespace Embedded {",
                "    static const std::map<std::string, Resource> resource_map = {"
            });
            foreach (var resourcePath in validResources)
            {
                var baseName = Path.GetFileName(resourcePath);
                var varName = SanitizeFileName(resourcePath);
                cppLines.Add($"        {{\"{baseName}\", {{{varName}_data, {varName}_size}}}}," );
            }
            cppLines.AddRange(new List<string>
            {
                "    };",
                "",
                "    const Resource& get(const std::string& name) {",
                "        return resource_map.at(name);",
                "    }",
                "}",
            });

            File.WriteAllLines(cppPath, cppLines);

            AnsiConsole.MarkupLine($"[bold green]Successfully generated `[bold]{headerPath}[/]` and `[bold]{cppPath}[/][/]`.");
        }

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

        public static void CreateTests()
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
            var config = ProjectConfigManager.LoadConfig();
            if (config == null)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] `package.toml` not found.");
                return; // Or throw an exception
            }

            Console.WriteLine("Test");

            if (!config.Dependencies.ContainsKey("googletest"))
            {
                config.Dependencies.Add("googletest", new Dependency
                {
                    Git = "https://github.com/google/googletest.git",
                    Tag = "v1.14.0"
                });
                ProjectConfigManager.SaveConfig(config);
                AnsiConsole.MarkupLine("[bold green]Added googletest dependency to package.toml.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]googletest dependency already exists in package.toml.[/]");
            }

            AnsiConsole.MarkupLine("[bold green]Tests created successfully.[/]");
        }
    }
}
