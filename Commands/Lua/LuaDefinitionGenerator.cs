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

        // Environment Variables
        sb.AppendLine("--- The current working directory");
        sb.AppendLine("---@type string");
        sb.AppendLine("forge.current_working_dir = \"\"");
        sb.AppendLine();

        return sb.ToString();
      }
    }
}
