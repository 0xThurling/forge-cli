using System.Diagnostics;
using System.Text;
using Spectre.Console;

namespace forge.ForgeEngine.CoreUtils;

public static partial class CoreUtils
{
  [System.Text.RegularExpressions.GeneratedRegex(@"\b_\w+\s*\(")]
  private static partial System.Text.RegularExpressions.Regex HiddenFunctions();

  [System.Text.RegularExpressions.GeneratedRegex(@"namespace\s+(\w+)\s*\{")]
  private static partial System.Text.RegularExpressions.Regex NamespaceGlobal();

  /// <summary>
  /// Detects the current Linux distribution from /etc/os-release.
  /// </summary>
  /// <returns>
  /// The canonical distribution name (e.g., "ubuntu", "arch", "fedora").
  /// </returns>
  public static string GetLinuxDistro()
  {
    var dict = new Dictionary<string, string>();
    try
    {
      var lines = File.ReadAllLines("/etc/os-release");

      foreach (var line in lines)
      {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
          continue;

        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
          var key = parts[0].Trim();
          var value = parts[1].Trim().Trim('"'); // remove quotes
          dict[key] = value;
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error reading distro info: " + ex.Message);
      return "unknown";
    }

    return dict.TryGetValue("NAME", out var name) ? DistroName(name) : "unknown";
  }

  /// <summary>
  /// Detects the current operating system.
  /// </summary>
  /// <returns>"linux", "macos", or "windows".</returns>
  public static string GetOperatingSystem()
  {
    if (OperatingSystem.IsLinux()) return "linux";
    if (OperatingSystem.IsMacOS()) return "macos";
    if (OperatingSystem.IsWindows()) return "windows";
    return string.Empty;
  }

  private static string DistroName(string name) => name switch
  {
    var n when n.Contains("arch", StringComparison.OrdinalIgnoreCase) => "arch",
    var n when n.Contains("debian", StringComparison.OrdinalIgnoreCase) => "debian",
    var n when n.Contains("ubuntu", StringComparison.OrdinalIgnoreCase) => "ubuntu",
    var n when n.Contains("mint", StringComparison.OrdinalIgnoreCase) => "mint",
    var n when n.Contains("kali", StringComparison.OrdinalIgnoreCase) => "kali",
    var n when n.Contains("red hat", StringComparison.OrdinalIgnoreCase) => "redhat",
    var n when n.Contains("fedora", StringComparison.OrdinalIgnoreCase) => "fedora",
    var n when n.Contains("centos", StringComparison.OrdinalIgnoreCase) => "centos",
    var n when n.Contains("rocky", StringComparison.OrdinalIgnoreCase) => "rocky",
    var n when n.Contains("manjaro", StringComparison.OrdinalIgnoreCase) => "manjaro",
    var n when n.Contains("garuda", StringComparison.OrdinalIgnoreCase) => "garuda",
    var n when n.Contains("alpine", StringComparison.OrdinalIgnoreCase) => "alpine",
    var n when n.Contains("amazon", StringComparison.OrdinalIgnoreCase) => "amazon",
    var n when n.Contains("nixos", StringComparison.OrdinalIgnoreCase) => "nixos",
    _ => "unknown"
  };

  /// <summary>
  /// Installs system packages using the specified package manager.
  /// </summary>
  /// <param name="hasPass">Whether sudo password is required.</param>
  /// <param name="packageManager">The package manager to use.</param>
  /// <param name="packages">List of package names to install.</param>
  /// <param name="pass">Optional sudo password.</param>
  public static void InstallPackages(bool hasPass, string packageManager, List<string> packages, string? pass = null)
  {
    // Build the command based on package manager
    string commandArgs = packageManager switch
    {
      "brew" or "winget" or "apt-get" or "choco" => $"install {string.Join(" ", packages)}",
      "pacman" => $"-S {string.Join(" ", packages)} --noconfirm",
      _ => string.Join(" ", packages)
    };
    AnsiConsole.MarkupLineInterpolated($"[cyan]Package Manager:[/] {packageManager}");
    AnsiConsole.MarkupLineInterpolated($"[cyan]Packages:[/] {string.Join(", ", packages)}");
    AnsiConsole.MarkupLineInterpolated($"[cyan]Requires sudo:[/] {hasPass}");
    var startInfo = new ProcessStartInfo
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true,
    };
    // Determine if we need sudo
    bool needsSudo = hasPass && packageManager switch { "brew" or "apt-get" or "pacman" => true, _ => false };

    if (needsSudo)
    {
      startInfo.FileName = "sudo";
      startInfo.Arguments = packageManager == "pacman"
        ? $"-S {commandArgs}"
        : $"-S {packageManager} {commandArgs}";

      if (!string.IsNullOrEmpty(pass))
      {
        startInfo.RedirectStandardInput = true;
      }
    }
    else
    {
      startInfo.FileName = packageManager;
      startInfo.Arguments = commandArgs;
    }
    AnsiConsole.MarkupLine("[yellow]Executing:[/] " + (needsSudo ? "sudo " : "") +
      (packageManager == "pacman" ? $"pacman {commandArgs}" : $"{packageManager} {commandArgs}"));
    using var process = Process.Start(startInfo) ??
      throw new Exception("Failed to start package manager process.");
    // Read output streams to prevent blocking
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    // If password required, send it
    if (needsSudo && !string.IsNullOrEmpty(pass))
    {
      process.StandardInput.WriteLine(pass);
      process.StandardInput.Close();
    }
    process.WaitForExit();
    string output = outputTask.Result;
    string error = errorTask.Result;
    // Log results
    if (!string.IsNullOrWhiteSpace(output))
    {
      AnsiConsole.MarkupLine("[dim]STDOUT:[/]");
      foreach (var line in output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
      {
        AnsiConsole.MarkupLineInterpolated($"  [dim]{line}[/]");
      }
    }
    if (!string.IsNullOrWhiteSpace(error))
    {
      AnsiConsole.MarkupLine("[red]STDERR:[/]");
      foreach (var line in error.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
      {
        AnsiConsole.MarkupLineInterpolated($"  [red]{line}[/]");
      }
    }
    if (process.ExitCode == 0)
    {
      AnsiConsole.MarkupLine("[green]✓ Packages installed successfully[/]");
    }
    else
    {
      AnsiConsole.MarkupLineInterpolated($"[bold red]✗ Package installation failed (exit code: {process.ExitCode})[/]");
      throw new Exception($"Package installation failed: {error}");
    }
  }

  public static void GenerateLibraryHeaders()
  {
    var srcDir = "src";
    var includeDir = "include";

    if (!Directory.Exists(srcDir))
    {
      AnsiConsole.MarkupLineInterpolated($"[bold yellow]Warning:[/] src/ directory not found. Skipping header generation.");
      return;
    }

    Directory.CreateDirectory(includeDir);

    var cppFiles = Directory.GetFiles(srcDir, "*.cpp");

    if (cppFiles.Length == 0)
    {
      AnsiConsole.MarkupLineInterpolated($"[bold yellow]Warning:[/] No .cpp files found in src/. Skipping header generation.");
      return;
    }

    AnsiConsole.MarkupLine("[cyan] Generating library headers [/]");

    foreach (var cppFile in cppFiles)
    {
      var fileName = Path.GetFileNameWithoutExtension(cppFile);
      var headerFileName = $"{fileName}.hpp";
      var headerPath = Path.Combine(includeDir, headerFileName);

      var content = File.ReadAllText(cppFile);
      var declarations = ExtractDeclarations(content);

      if (declarations.Count > 0)
      {
        var headerContent = new StringBuilder();
        headerContent.AppendLine($"#pragma once");
        headerContent.AppendLine();
        headerContent.AppendLine("// Auto Generated from src/{fileName}.cpp");
        headerContent.AppendLine("// Warning: Manual edits may be overwritten");
        headerContent.AppendLine();

        var includes = ExtractIncludes(content);
        foreach (var inc in includes)
        {
          headerContent.AppendLine(inc);
        }

        if (includes.Count > 0)
        {
          headerContent.AppendLine();
        }

        var namespaceMatch = NamespaceGlobal().Match(content);
        if (namespaceMatch.Success)
        {
          var ns = namespaceMatch.Groups[1].Value;
          headerContent.AppendLine($"namespace {ns} {{");
          headerContent.AppendLine();
        }

        foreach (var decl in declarations)
        {
          headerContent.AppendLine(decl);
        }

        if (namespaceMatch.Success)
        {
          headerContent.AppendLine();
          headerContent.AppendLine("}");
        }

        File.WriteAllText(headerPath, headerContent.ToString());
        AnsiConsole.MarkupLine($"[green]Generated:[/] {headerPath}");
      }
      else
      {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] No declarations found in {fileName}.cpp. Skipping.");
      }
    }

    AnsiConsole.MarkupLine($"[bold green]Header generation complete. Found {cppFiles.Length} source file(s).[/]");
  }

  private static List<string> ExtractIncludes(string source)
  {
    var includes = new List<string>();
    var pattern = @"#include\s+[<""]([^>""]+)[>""]";

    var matches = System.Text.RegularExpressions.Regex.Matches(source, pattern);
    foreach (System.Text.RegularExpressions.Match match in matches)
    {
      includes.Add(match.Value);
    }

    return includes;
  }

  private static List<string> ExtractDeclarations(string source)
  {
    var declarations = new List<string>();

    var funcPattern = @"^\s*(\w+[\s\*&]+)+(\w+)\s*\(([^)]*)\)\s*;";

    var lines = source.Split('\n');

    foreach (var line in lines)
    {
      if (line.Contains('{')) continue;

      var match = System.Text.RegularExpressions.Regex.Match(line, funcPattern);
      if (match.Success)
      {
        var declaration = line.Trim();

        if (HiddenFunctions().IsMatch(declaration))
        {
          declarations.Add(declaration);
        }
      }
    }

    var defPattern = @"^\s*(\w+[\s\*&]+)+(\w+)\s*\(([^)]*)\)\s*\{";
    foreach (var line in lines)
    {
      var match = System.Text.RegularExpressions.Regex.Match(line, defPattern);
      if (match.Success)
      {
        // Extract function signature and convert to declaration
        var returnType = match.Groups[1].Value.Trim();
        var funcName = match.Groups[2].Value;
        var args = match.Groups[3].Value;

        var declaration = $"{returnType} {funcName}({args});";

        // Check if we already have this declaration
        if (!declarations.Contains(declaration) && !declaration.Contains("::"))
        {
          declarations.Add(declaration);
        }
      }
    }
    return declarations;
  }
}
