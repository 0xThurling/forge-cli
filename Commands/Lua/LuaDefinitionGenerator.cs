using System.Text;

namespace cpm.Commands.Lua
{
    public class LuaDefinitionGenerator : LuaEngine
    {
      public static string GenerateDefinitions() {
        var sb = new StringBuilder();

        sb.AppendLine("---@meta");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("--- Provides access to cpm variables and functions for the Lua LSP");
        sb.AppendLine("--- This file is automatically generally");
        sb.AppendLine("---");
  
        // Function definitions
        // --- Global cpm API object
        // ---@class cpm
        // cpm = {}
        sb.AppendLine("--- Global cpm API object");
        sb.AppendLine("---@class cpm");
        sb.AppendLine("cpm = {}");
        sb.AppendLine();

        // --- cpm logging utility
        // ---@class cpm.log
        // cpm = {}
        sb.AppendLine("--- cpm logging utility");
        sb.AppendLine("---@class cpm.log");
        sb.AppendLine("cpm.log = {}");
        sb.AppendLine();

        // --- cpm logging information 
        sb.AppendLine("--- Logs an information message to the console");
        sb.AppendLine("---@param message string The message to log.");
        sb.AppendLine("function cpm.log.information(message) end");
        sb.AppendLine();

        // --- cpm pull repo 
        sb.AppendLine("--- Logs an information message to the console");
        sb.AppendLine("---@param repo_url string The URL for the github repository.");
        sb.AppendLine("---@return string The path location where it get's saved");
        sb.AppendLine("function cpm.pull_repo(repo_url) end");
        sb.AppendLine();

        // Environment Variables
        sb.AppendLine("--- The current working directory");
        sb.AppendLine("---@type string");
        sb.AppendLine("current_working_dir = \"\"");
        sb.AppendLine();

        return sb.ToString();
      }
    }
}
