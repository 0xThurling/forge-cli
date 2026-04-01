using System.Diagnostics;
using System.Text.Json;
using forge.Models;
using Spectre.Console;

namespace forge.Commands.Conan;

public class ConanDependencyChecker
{
  /// <summary>
  ///  Gets transitive dependencies for a conan package by running `conan graph info`
  /// </summary>
  public async Task<List<string>> GetTransitiveDependenciesAsync(string package)
  {
    var deps = new List<string>();

    try
    {
      var processInfo = new ProcessStartInfo("conan", $"graph info --requires={package} --format=json")
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

        // Navigate to the requires section
        if (root.TryGetProperty("graph", out var graph) && graph.TryGetProperty("requires", out var requires))
        {
          foreach (var req in requires.EnumerateArray())
          {
            if (req.TryGetProperty("ref", out var refProp))
            {
              var refValue = refProp.GetString();
              if (!string.IsNullOrEmpty(refValue))
              {
                var packageName = refValue.Split('/').First();
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

    foreach (var conanDep in config.ConanDependencies.Keys)
    {
      var transitiveDeps = await GetTransitiveDependenciesAsync(conanDep);

      foreach (var transDep in transitiveDeps)
      {
        if (gitDeps.Contains(transDep))
        {
          warnings.Add($"'{conanDep}' (Conan) pulls in '{transDep}' transitively, but you also have it as a git dependency.");
        }
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
