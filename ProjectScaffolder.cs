using forge.Commands.Lua;
using Spectre.Console;

namespace forge;

/// <summary>
/// Writes the files that make a directory a Forge project. Shared by
/// <c>forge create</c> (a new directory) and <c>forge init</c> (the current one).
/// </summary>
internal static class ProjectScaffolder
{
  /// <summary>
  /// Creates the layout, <c>forge.lua</c>, a starter source file and — when the
  /// directory has none — a <c>.gitignore</c>.
  /// </summary>
  /// <remarks>
  /// Existing files are never overwritten: initialising a directory that
  /// already has sources keeps them, and an existing <c>.gitignore</c> is left
  /// to its owner (<c>forge doctor --fix</c> adds anything missing).
  /// </remarks>
  public static void Scaffold(
    string directory, string name, string type, string standard, bool testing)
  {
    Directory.CreateDirectory(directory);
    Directory.CreateDirectory(Path.Combine(directory, "src"));
    Directory.CreateDirectory(Path.Combine(directory, "external"));
    Directory.CreateDirectory(Path.Combine(directory, "assets"));
    Directory.CreateDirectory(Path.Combine(directory, ".config"));
    Directory.CreateDirectory(Path.Combine(directory, ".config", "forge"));
    Directory.CreateDirectory(Path.Combine(directory, ".config", "forge", "commands"));
    Directory.CreateDirectory(Path.Combine(directory, ".config", "forge", "build"));
    Directory.CreateDirectory(Path.Combine(directory, ".config", "forge", "templates"));

    // The editor stubs for the Lua API.
    LuaEngine.WriteEnvironmentDefinitions(directory);

    // A starter source file, for the executable case, when there is none.
    if (type == "executable")
    {
      var mainPath = Path.Combine(directory, "src", "main.cpp");
      if (!File.Exists(mainPath))
      {
        File.WriteAllText(mainPath,
          "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, C++ World!\" << std::endl;\n    return 0;\n}");
      }
    }

    var forgeLuaPath = Path.Combine(directory, "forge.lua");
    if (!File.Exists(forgeLuaPath))
      File.WriteAllText(forgeLuaPath, Config(name, type, standard, testing));

    var gitignorePath = Path.Combine(directory, ".gitignore");
    if (!File.Exists(gitignorePath))
      File.WriteAllText(gitignorePath, SourceFiles.DefaultGitIgnore());
  }

  /// <summary>The <c>forge.lua</c> written for a new project.</summary>
  public static string Config(string name, string type, string standard, bool testing)
  {
    var installHeaders = type == "library" ? "install_headers = true," : "";
    return $@"return {{
  project = {{
    name = ""{name}"",
    type = ""{type}"",
    standard = ""{standard}"",
    {installHeaders}
  }},
  testing = {(testing ? "true" : "false")},
  dependencies = {{
   direct = {{}},
   conan = {{}}
  }},
  resources = {{
    files = {{}}
  }},
  scripts = {{}},
  features = {{}}
}}";
  }
}
