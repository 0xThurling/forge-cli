using System.Text;
using forge.Models;

namespace forge.Commands.Lua
{
  public static class LuaDefinitionGenerator
  {
    private static readonly List<LuaDefinitionCategory> _categories = [];

    static LuaDefinitionGenerator()
    {
      InitializeDefinitions();
    }

    // TODO: Increase support for more package managers and more distrobutions
    private static void InitializeDefinitions()
    {
      // Core forge object
      _categories.Add(new LuaDefinitionCategory("forge", "Global forge API object")
          .AddConstantInformation("class", "forge"));

      _categories.Add(new LuaDefinitionCategory("forge.log",
            "Forge logging utility")
          .AddConstantInformation("class", "forge.log"));

      _categories.Add(new LuaDefinitionCategory("forge.config",
            "Access Forge's config")
          .AddConstantInformation("class", "forge.config"));

      // Functions - Generic
      _categories.Add(new LuaDefinitionCategory("forge.add_cmake",
            "Adds custom CMake commands to the generated CMakeLists.txt",
            new LuaParameter("snippet", "string", "The custom CMake command")));

      _categories.Add(new LuaDefinitionCategory("forge.log.info",
            "Logs an information message to the console",
            new LuaParameter("message", "string", "The message to log")));

      _categories.Add(new LuaDefinitionCategory("forge.log.warn",
            "Logs a warning message to the console",
            new LuaParameter("message", "string", "The message to log")));

      _categories.Add(new LuaDefinitionCategory("forge.log.error",
            "Logs a error message to the console",
            new LuaParameter("message", "string", "The message to log")));

      _categories.Add(new LuaDefinitionCategory("forge.pull_repo",
            "Clones a Git repository to external/",
            new LuaParameter("repo_url", "string", "The URL for the GitHub repository"),
            new LuaParameter("tag", "string", "The URL for the GitHub repository"))
          .AddReturn(new LuaParameterReturn("string", "The path location where it's saved'")));

      _categories.Add(new LuaDefinitionCategory("forge.get_packages",
            "Installs packages using the Specified package manager",
            new LuaParameter("password", "string", "Password or 'nopass'"),
            new LuaParameter("package_manager", "string", "The package manager"),
            new LuaParameter("packages", "string[]", "List of packages"))
          .AddReturn(new LuaParameterReturn("number", "0 on success")));

      // Config Functions
      _categories.Add(new LuaDefinitionCategory("forge.config.get",
            "Gets a config value by key",
            new LuaParameter("key", "string", "Dot-notation key (e.g., 'project.name')"))
          .AddReturn(new LuaParameterReturn("string", "The config value")));

      _categories.Add(new LuaDefinitionCategory("forge.config.has_feature",
            "Checks if a feature is enabled",
            new LuaParameter("feature", "string", "Feature name"))
          .AddReturn(new LuaParameterReturn("boolean", "True if enabled")));

      _categories.Add(new LuaDefinitionCategory("forge.config.get_feature_option",
            "Gets a feature option value",
            new LuaParameter("feature", "string", "Feature name"),
            new LuaParameter("option", "string", "Option name"),
            new LuaParameter("default", "string", "Default value if not set"))
          .AddReturn(new LuaParameterReturn("string", "The option value")));

      // Environment tables
      // Operating System information
      _categories.Add(new LuaDefinitionCategory("forge.os",
            "Operating System information")
          .AddConstantInformation("class", "forge.os"));

      _categories.Add(new LuaDefinitionCategory("forge.os.current",
            "The current operating system")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.os.windows",
            "The Windows operating system")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.os.macos",
            "The MacOS operating system")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.os.linux",
            "The Linux operating system")
          .AddConstantInformation("type", "string"));

      // Distrobution information
      _categories.Add(new LuaDefinitionCategory("forge.distro",
            "Linux distribution information")
          .AddConstantInformation("class", "forge.distro"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.my_distro",
            "Current Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.arch",
            "The Arch Linux distrobution... btw")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.nixos",
            "The NixOs Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.debian",
            "The Debian Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.ubuntu",
            "The Ubuntu Linux Distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.manjaro",
            "The Manjaro Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.fedora",
            "The Fedora Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.distro.unknown",
            "Unknown Linux distrobution")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager",
            "Package manager constants")
          .AddConstantInformation("class", "forge.package_manager"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.no_pass",
            "Use no password for package manager")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.winget",
            "The WinGet package manager for Windows")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.chocolatey",
            "The Chocolatey package manager for Windows")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.brew",
            "The Homebrew package manager for MacOs")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.pacman",
            "The Pacman package manager for Arch")
          .AddConstantInformation("type", "string"));

      _categories.Add(new LuaDefinitionCategory("forge.package_manager.aptget",
            "The APT package manager for Debian/Ubuntu")
          .AddConstantInformation("type", "string"));

      // Extraction/Download/Fetching
      _categories.Add(new LuaDefinitionCategory("forge.download",
            "Downloads a file from a URL",
            new LuaParameter("url", "string", "The URL to download"),
            new LuaParameter("output", "string", "The output file path")));

      _categories.Add(new LuaDefinitionCategory("forge.extract",
            "Extracts an archive file",
            new LuaParameter("archive", "string", "Path to archive file"),
            new LuaParameter("output", "string", "Output directory"),
            new LuaParameter("string_components", "number", "The number of path components to strip (default: 1)")));

      _categories.Add(new LuaDefinitionCategory("forge.fetch",
            "Downloads and extracts an archive in one step",
            new LuaParameter("url", "string", "The URL to fetch"))
          .AddReturn(new LuaParameterReturn("string", "The path to the newly fetched directory")));


      // General Information
      _categories.Add(new LuaDefinitionCategory("forge.current_working_dir",
            "The current working directory")
          .AddConstantInformation("type", "string"));
    }

    public static string GenerateDefinitions()
    {
      var sb = new StringBuilder();

      sb.AppendLine("---@meta");
      sb.AppendLine();
      sb.AppendLine("---");
      sb.AppendLine("--- Forge Lua API Type Definitions");
      sb.AppendLine("--- Auto-generated for Lua Language Server (lua_ls)");
      sb.AppendLine("---");
      sb.AppendLine();

      foreach (var category in _categories)
      {
        sb.AppendLine(category.ToDefinition());
      }

      return sb.ToString();
    }
  }
}
