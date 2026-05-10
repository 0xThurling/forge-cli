using System.Diagnostics;
using System.Text;
using System.Text.Json;
using forge.Models;
using Spectre.Console;

namespace forge.Commands.Conan;

public class ConanDependencyChecker
{
  /// <summary>
  ///  Gets transitive dependencies for a conan package by running `conan graph info`
  /// </summary>
  public async Task<List<string>> GetTransitiveDependenciesAsync(string package, string conanfilePath)
  {
    var deps = new List<string>();

    try
    {
      var processInfo = new ProcessStartInfo("conan", $"graph info {conanfilePath} --format=json")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      using var process = Process.Start(processInfo);
      if (process == null) return deps;

      var output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();

      // Parse JSON to extract dependencies
      if (!string.IsNullOrEmpty(output))
      {
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        // Navigate to the nodes section (Conan 2.x format)
        if (root.TryGetProperty("graph", out var graph) && graph.TryGetProperty("nodes", out var nodes))
        {
          foreach (var node in nodes.EnumerateObject())
          {
            if (node.Value.TryGetProperty("ref", out var refProp))
            {
              var refValue = refProp.GetString();
              if (!string.IsNullOrEmpty(refValue) && refValue.Contains('/'))
              {
                // Extract package name (before the /)
                var packageName = refValue.Split('/')[0];
                deps.Add(packageName);
              }
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[dim]Could not get dependencies for {package}: {ex.Message}[/]");
    }

    return deps;
  }

  /// <summary>
  ///  Checks for conflicts between git and conan dependencies
  /// </summary>
  public async Task CheckForConflicts(ProjectConfig config)
  {
    var gitDeps = config.Dependencies.Keys.ToHashSet();
    var warnings = new List<string>();

    try
    {
      // Check each conan dependency separately
      foreach (var conanDep in config.ConanDependencies.Keys)
      {
        var version = config.ConanDependencies[conanDep];
        
        // Generate a conanfile.txt with just this one dependency
        var conanfile = new StringBuilder();
        conanfile.AppendLine("[requires]");
        conanfile.AppendLine($"{conanDep}/{version}");
        conanfile.AppendLine("\n[generators]\nCMakeDeps\nCMakeToolchain\n\n[layout]\ncmake_layout");

        var conanfilePath = Path.Combine(Path.GetTempPath(), "forge_doctor_conanfile.txt");
        await File.WriteAllTextAsync(conanfilePath, conanfile.ToString());

        var transitiveDeps = await GetTransitiveDependenciesAsync($"{conanDep}/{version}", conanfilePath);

        foreach (var transDep in transitiveDeps)
        {
          if (gitDeps.Contains(transDep))
          {
            warnings.Add($"'{conanDep}' (Conan) pulls in '{transDep}' transitively, but you also have it as a git dependency.");
          }
        }
      }
    }
    finally
    {
      // Clean up temp file
      var conanfilePath = Path.Combine(Path.GetTempPath(), "forge_doctor_conanfile.txt");
      if (File.Exists(conanfilePath))
      {
        File.Delete(conanfilePath);
      }
    }

    if (warnings.Count > 0)
    {
      AnsiConsole.MarkupLine("[yellow]⚠️  Dependency Conflicts Detected:[/]");
      foreach (var warning in warnings)
      {
        AnsiConsole.MarkupLine($"  [yellow]-[/] {warning}");
      }
      AnsiConsole.MarkupLine("[dim]Consider removing the git (direct) dependency or using the Conan Version.[/]");
    }
    else
    {
      AnsiConsole.MarkupLine("[green]✅ No dependency conflicts detected[/]");
    }
  }
}
