using System.Text;
using DotMake.CommandLine;
using forge.Models;
using Spectre.Console;

namespace forge.Commands;

[CliCommand(
  Name = "config",
  Description = "Manage project configuration",
  Parent = typeof(RootCommand)
)]
public class ConfigCommand
{

  [CliCommand(
    Name = "migrate",
    Description = "Migrate package.toml to forge.lua"
  )]
  public class MigrateCommand
  {
    public async Task<int> Run()
    {
      var config = ProjectConfigManager.TomlConfigLoader();
      if (config is null)
      {
        AnsiConsole.MarkupLine($"[red]No package.toml found to migrate[/]");
        return 1;
      }

      // Generate forge.lua
      var luaContent = GenerateLuaConfig(config);

      // Write to file
      File.WriteAllText("forge.lua", luaContent);

      AnsiConsole.MarkupLine("[green]Migration complete![/]");
      AnsiConsole.MarkupLine("[yellow]Created forge.lua from package.toml[/]");
      AnsiConsole.MarkupLine("[yellow]You can now delete package.toml when ready[/]");
      return 0;
    }

    private static string GenerateLuaConfig(ProjectConfig config)
    {
      var sb = new StringBuilder();

      sb.AppendLine("-- Forge Project Configuration");
      sb.AppendLine("-- This file replaces package.toml");
      sb.AppendLine();
      sb.AppendLine("return {");

      // Project section
      sb.AppendLine("    project = {");
      sb.AppendLine($"        name = \"{config.Project.Name}\",");
      sb.AppendLine($"        type = \"{config.Project.Type}\",");
      sb.AppendLine($"        standard = \"{config.Project.Standard}\",");
      sb.AppendLine($"        install_headers = {config.Project.InstallHeaders.ToString().ToLower()},");
      sb.AppendLine("    },");

      // Dependencies
      if (config.Dependencies.Count > 0)
      {
        sb.AppendLine();
        sb.AppendLine("    dependencies = {");
        sb.AppendLine("        git = {");
        foreach (var dep in config.Dependencies)
        {
          sb.AppendLine($"            {dep.Key} = {{");
          if (!string.IsNullOrEmpty(dep.Value.Git))
            sb.AppendLine($"                git = \"{dep.Value.Git}\", ");
          if (!string.IsNullOrEmpty(dep.Value.Tag))
            sb.AppendLine($"                tag = \"{dep.Value.Tag}\", ");
          if (!string.IsNullOrEmpty(dep.Value.Target))
            sb.AppendLine($"                target = \"{dep.Value.Target}\",");
          sb.AppendLine("            },");
        }
        sb.AppendLine("        },");
        sb.AppendLine("    },");
      }

      // Conan dependencies
      if (config.ConanDependencies.Count > 0)
      {
        if (config.Dependencies.Count > 0)
        {
          // Add conan to existing dependencies block
          sb.AppendLine("        conan = {");
          foreach (var dep in config.ConanDependencies)
          {
            sb.AppendLine($"            {dep.Key} = \"{dep.Value}\","
);
          }
          sb.AppendLine("        },");
          sb.AppendLine("    },");
        }
        else
        {
          // Create new dependencies block with conan
          sb.AppendLine();
          sb.AppendLine("    dependencies = {");
          sb.AppendLine("        conan = {");
          foreach (var dep in config.ConanDependencies)
          {
            sb.AppendLine($"            {dep.Key} = \"{dep.Value}\","
);
          }
          sb.AppendLine("        },");
          sb.AppendLine("    },");
        }
      }

      // Resources
      if (config.Resources.Files.Count > 0)
      {
        sb.AppendLine();
        sb.AppendLine("    resources = {");
        sb.AppendLine("        files = {");
        foreach (var file in config.Resources.Files)
        {
          sb.AppendLine($"            \"{file}\",");
        }
        sb.AppendLine("        },");
        sb.AppendLine("    },");
      }

      // Scripts
      if (config.Scripts.Count > 0)
      {
        sb.AppendLine();
        sb.AppendLine("    scripts = {");
        foreach (var script in config.Scripts)
        {
          sb.AppendLine($"        [\"{script.Key}\"] = \"{script.Value}\",");
        }
        sb.AppendLine("    },");
      }

      sb.AppendLine("}");

      return sb.ToString();
    }
  }
}
