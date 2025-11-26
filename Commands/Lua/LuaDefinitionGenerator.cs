using System.Text;

namespace forge.Commands.Lua
{
    public static class LuaDefinitionGenerator 
    {
      public static string GenerateDefinitions() {
        var sb = new StringBuilder();

        sb.AppendLine("---@meta");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("--- Provides access to forge variables and functions for the Lua LSP");
        sb.AppendLine("--- This file is automatically generally");
        sb.AppendLine("---");
  
        // Function definitions
        // --- Global forge API object
        // ---@class forge
        // forge = {}
        sb.AppendLine("--- Global forge API object");
        sb.AppendLine("---@class forge");
        sb.AppendLine("forge = {}");
        sb.AppendLine();

        // --- forge logging utility
        // ---@class forge.log
        // forge = {}
        sb.AppendLine("--- forge logging utility");
        sb.AppendLine("---@class forge.log");
        sb.AppendLine("forge.log = {}");
        sb.AppendLine();

        // --- forge logging information 
        sb.AppendLine("--- Logs an information message to the console");
        sb.AppendLine("---@param message string The message to log.");
        sb.AppendLine("function forge.log.info(message) end");
        sb.AppendLine();

        // --- forge pull repo 
        sb.AppendLine("--- Logs an information message to the console");
        sb.AppendLine("---@param repo_url string The URL for the github repository.");
        sb.AppendLine("---@return string The path location where it get's saved");
        sb.AppendLine("function forge.pull_repo(repo_url) end");
        sb.AppendLine();

        // Get packages
        sb.AppendLine("--- Gets the packages with a specific package manager");
        sb.AppendLine("---@param package_manager string The Package Manager Specified.");
        sb.AppendLine("---@param packages string[] The list of package you wish to download.");
        sb.AppendLine("---@return string The path location where it get's saved");
        sb.AppendLine("function forge.get_packages(package_manager, packages) end");
        sb.AppendLine();

        // Environment Variables
        sb.AppendLine("--- The current working directory");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.current_working_dir = \"\"");
        sb.AppendLine();

        // Operating Systems
        sb.AppendLine("--- The Operating System for the Build");
        sb.AppendLine("---@class forge.os");
        sb.AppendLine("forge.os = {}");
        sb.AppendLine();

        sb.AppendLine("--- The current operating system");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.os.current = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The Windows operating system");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.os.windows = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The MacOS operating system");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.os.macos = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The Linux Operating System");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.os.linux = \"\"");
        sb.AppendLine();

        // Package managers
        sb.AppendLine("--- The package managers for various operating systems");
        sb.AppendLine("---@class forge.package_manager");
        sb.AppendLine("forge.package_manager = {}");
        sb.AppendLine();

        sb.AppendLine("--- The WinGet package manager for Windows");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.package_manager.winget = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The Chocolatey package manager for Windows");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.package_manager.chocolatey = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The Homebrew package manager for MacOS");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.package_manager.brew = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The Pacman Package Manager for Arch");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.package_manager.pacman = \"\"");
        sb.AppendLine();

        sb.AppendLine("--- The APT package manager for Debian");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.package_manager.aptget = \"\"");
        sb.AppendLine();

        return sb.ToString();
      }
    }
}
