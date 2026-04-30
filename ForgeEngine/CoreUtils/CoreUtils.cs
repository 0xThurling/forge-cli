using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace forge.ForgeEngine.CoreUtils;

public static partial class CoreUtils
{
  [GeneratedRegex(@"namespace\s+(\w+)\s*\{")]
  private static partial Regex NamespacePattern();

  [GeneratedRegex(@"/\*[\s\S]*?\*/")]
  private static partial Regex BlockCommentsPatterns();

  [GeneratedRegex(@"//.*$", RegexOptions.Multiline)]
  private static partial Regex LineCommentsPattern();

  [GeneratedRegex(@"\b_impl\b")]
  private static partial Regex ImplFunctionPattern();

  [GeneratedRegex(@"^namespace\s+detail\b")]
  private static partial Regex DetailNamespacePattern();

  [GeneratedRegex(@"#include\s+""([^""]+)""")]
  private static partial Regex LocalIncludesPattern();

  [GeneratedRegex(@"#include\s+<([^>]+)>")]
  private static partial Regex SystemIncludesPattern();

  [GeneratedRegex(@"^[\w:]+[\s\*&<>]+\w+\s*\([^;]+\)\s*;$")]
  private static partial Regex FunctionPointerPattern();

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
      AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] src/ directory not found. Skipping header generation.");
      return;
    }
    Directory.CreateDirectory(includeDir);
    var cppFiles = Directory.GetFiles(srcDir, "*.cpp", SearchOption.AllDirectories);

    if (cppFiles.Length == 0)
    {
      AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] No .cpp files found in src/. Skipping header generation.");
      return;
    }
    AnsiConsole.MarkupLine("[cyan]--- Generating library headers ---[/]");
    foreach (var cppFile in cppFiles)
    {
      var fileName = Path.GetFileNameWithoutExtension(cppFile);
      var headerFileName = $"{fileName}.hpp";
      var headerPath = Path.Combine(includeDir, headerFileName);
      var content = File.ReadAllText(cppFile);

      // Skip if manual header exists
      if (File.Exists(headerPath))
      {
        AnsiConsole.MarkupLine($"[dim]Skipping {headerPath} - manual header exists[/]");
        continue;
      }
      var declarations = ExtractDeclarations(content);
      var includes = ExtractIncludes(content);
      var namespaceInfo = ExtractNamespace(content);
      if (declarations.Count > 0)
      {
        var headerContent = new StringBuilder();
        headerContent.AppendLine("#pragma once");
        headerContent.AppendLine();
        headerContent.AppendLine($"// Auto-generated from src/{fileName}.cpp");
        headerContent.AppendLine("// WARNING: Manual edits may be overwritten");
        headerContent.AppendLine();
        foreach (var inc in includes.Distinct().OrderBy(i => i))
          headerContent.AppendLine(inc);

        if (includes.Count > 0)
          headerContent.AppendLine();
        if (!string.IsNullOrEmpty(namespaceInfo))
        {
          headerContent.AppendLine($"namespace {namespaceInfo} {{");
          headerContent.AppendLine();
        }
        foreach (var decl in declarations)
          headerContent.AppendLine(decl);
        if (!string.IsNullOrEmpty(namespaceInfo))
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

  private static string ExtractNamespace(string source)
  {
    source = BlockCommentsPatterns().Replace(source, "");
    source = LineCommentsPattern().Replace(source, "");
    var match = NamespacePattern().Match(source);
    return match.Success ? match.Groups[1].Value : "";
  }

  private static List<string> ExtractIncludes(string source)
  {
    var includes = new List<string>();

    foreach (Match match in SystemIncludesPattern().Matches(source))
    {
      var header = match.Groups[1].Value;
      if (IsStandardHeader(header))
        includes.Add(match.Value);
    }

    foreach (Match match in LocalIncludesPattern().Matches(source))
    {
      var header = match.Groups[1].Value;
      if (!header.EndsWith(".cpp") && !header.Contains("_impl"))
        includes.Add(match.Value);
    }

    return includes;
  }

  private static bool IsStandardHeader(string header)
  {
    var standardHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      "string", "vector", "map", "set", "unordered_map", "unordered_set",
      "list", "deque", "array", "tuple", "optional", "variant", "any",
      "memory", "memory_resource", "functional", "algorithm", "chrono",
      "cstdint", "cstddef", "cstring", "cstdlib", "cmath", "complex",
      "iostream", "fstream", "sstream", "regex", "thread", "mutex",
      "atomic", "future", "promise", "condition_variable",
      "type_traits", "typeindex", "typeinfo",
      "initializer_list", "compare", "concepts",
      "coroutine", "stop_token", "semaphore", "latch", "barrier",
      "new", "typeinfo", "exception", "stdexcept", "system_error",
      "cassert", "cstdio", "ctime", "climits", "cfloat",
      "filesystem", "codecvt", "locale", "wchar.h", "uchar.h",
      "fmt/core.h", "fmt/format.h", "fmt/ostream.h", "fmt/ranges.h",
      "spdlog/spdlog.h", "spdlog/common.h", "spdlog/sinks/stdout_color_sinks.h",
      "gtest/gtest.h", "gmock/gmock.h",
      "nlohmann/json.hpp", "json/json.hpp",
      "boost/any.hpp", "boost/variant.hpp",
      "sqlite3.h", "yaml-cpp/yaml.h",
      "openssl/sha.h", "openssl/rsa.h",
      "windows.h", "winsock2.h", "ws2tcpip.h",
    };

    return standardHeaders.Contains(header) || header.StartsWith("std::") || header.Contains('/');
  }

  private static List<string> ExtractDeclarations(string source)
  {
    var declarations = new List<string>();

    // Remove comments
    source = BlockCommentsPatterns().Replace(source, "");
    source = LineCommentsPattern().Replace(source, "");

    // Extract various complex declarations
    ExtractTemplateFunctions(source, ref declarations);
    ExtractFunctionPointers(source, ref declarations);
    ExtractFunctionsWithPointerParams(source, ref declarations);
    ExtractTrailingReturnTypes(source, ref declarations);
    ExtractMemberPointers(source, ref declarations);
    ExtractSimpleFunctions(source, ref declarations);
    ExtractConcepts(source, ref declarations);
    ExtractOperators(source, ref declarations);
    ExtractUsingDeclarations(source, ref declarations);
    ExtractTypeAliases(source, ref declarations);

    // Remove duplicates and filter
    declarations = [.. declarations.Distinct()];
    declarations = [.. declarations.Where(d =>
      !ImplFunctionPattern().IsMatch(d) &&
      !DetailNamespacePattern().IsMatch(d)
    )];

    // Sort by: Templates, then by complexity
    return [.. declarations.OrderByDescending(d => d.Contains("template"))
      .ThenBy(d => d.Contains("auto"))
      .ThenBy(d => d.Length)];
  }

  private static void ExtractFunctionsWithPointerParams(string source, ref List<string> declarations)
  {
    source = BlockCommentsPatterns().Replace(source, "");
    source = LineCommentsPattern().Replace(source, "");

    foreach (var line in source.Split('\n'))
    {
      var trimmed = line.Trim();

      // Only process lines that end in ; and contain a function pointer param
      if (!trimmed.EndsWith(';') || !trimmed.Contains("(*")) continue;

      // Skip using aliases = handles by ExtractTypeAliases
      if (trimmed.StartsWith("using")) continue;

      if (trimmed.Contains("->")) continue;

      bool isDecl = FunctionPointerPattern().IsMatch(trimmed);

      if (isDecl && !declarations.Contains(trimmed))
        declarations.Add(trimmed);
    }
  }

  private static void ExtractTypeAliases(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"template\s*<\s*typename\s+(\w+)\s*>\s*using\s+(\w+)\s*=\s*([^;]+);",
      @"using\s+(\w+)\s*=\s*(?:typename\s+)?([^{;]+);",
      @"using\s+(\w+)\s*=\s*([\w:]+[\s\*&]+);",
    };
    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = match.Value.Trim();
        if (!decl.Contains("_impl") && !decl.Contains("detail::"))
          if (!declarations.Contains(decl))
            declarations.Add(decl);
      }
    }
  }

  private static void ExtractUsingDeclarations(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"using\s+(\w+)\s*=\s*([^;]+);",
      @"using\s+enum\s+(\w+)\s*;",
      @"using\s+(\w+)::(\w+)\s*;",
    };
    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = match.Value.Trim();
        if (!declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractOperators(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"operator\s*([+\-*/%^&|<<>>]=?|&&|\|\||==|!=|<=|>=|<=>|<\s*|>\s*)\s*\(([^)]*)\)\s*(?:const)?\s*;",
      @"operator\s*(?:\+\+|--|\+|!|~|&\s*|\*\s*)\s*\(\s*\)\s*(?:const)?\s*;",
      @"operator\s*\[\s*\]\s*\(([^)]*)\)\s*(?:const)?\s*;",
      @"operator\s*\(\s*\)\s*\(([^)]*)\)\s*(?:const)?\s*;",
      @"operator\s+(?:[\w:]+[\s\*&<>]*)\s*\(\s*\)\s*(?:const)?\s*(?:noexcept)?\s*;",
      @"operator\s*=\s*\(([^)]*)\)\s*(?:const)?\s*;",
      @"operator\s*<=>\s*\(([^)]*)\)\s*(?:const)?\s*;",
    };

    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = match.Value.Trim();
        if (!declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractConcepts(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"template\s*<\s*typename\s+(\w+)\s*>\s*concept\s+(\w+)\s*=\s*([^;]+);",
      @"template\s*<\s*typename\s+(\w+)\s*>\s*concept\s+(\w+)\s*requires\s+([^;]+);",
      @"template\s*<\s*typename\s+(\w+)\s*,\s*typename\s+(\w+)\s*>\s*concept\s+(\w+)\s*=\s*([^;]+);",
    };
    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = FormatConcept(match);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractSimpleFunctions(string source, ref List<string> declarations)
  {
    var lines = source.Split('\n');

    foreach (var line in lines)
    {
      // Skip template functions - handled separately 
      if (line.TrimStart().StartsWith("template")) continue;

      var trimmed = line.Trim();
      string? candidate = null;
      if (trimmed.EndsWith(';'))
      {
        candidate = trimmed; // declaration ending with semi-colon
      }
      else if (trimmed.Contains('{') && trimmed.Contains('('))
      {
        int braceIndex = trimmed.IndexOf('{');
        candidate = trimmed[..braceIndex].Trim();
        if (!candidate.EndsWith(';'))
        {
          candidate += ";";
        }
      }
      else
      {
        continue;
      }

      var patterns = new[]
      {
        @"(?:\[\[[^\]]+\]\]\s*)*(?:\w+\s+)*(\w+[\s\*&<>]+)(\w+)\s*\(([^)]*)\)\s*(?:const)?\s*(?:noexcept)?\s*(?:constexpr)?\s*(?:final)?\s*(?:override)?\s*;",
        @"virtual\s+(?:[\w:]+[\s\*&<>]*)(\w+)\s*\(([^)]*)\)\s*(?:const)?\s*(?:noexcept)?\s*(?:override)?\s*;",
        @"virtual\s+(?:[\w:]+[\s\*&<>]*)(\w+)\s*\(([^)]*)\)\s*=\s*0\s*;",
        @"static\s+(?:[\w:]+[\s\*&<>]*)(\w+)\s*\(([^)]*)\)\s*(?:const)?\s*(?:noexcept)?\s*;",
        @"explicit\s+(\w+)\s*\(([^)]*)\)\s*(?:noexcept)?\s*;",
        @"virtual?\s*~(\w+)\s*\(\s*\)\s*(?:noexcept)?\s*(?:override)?\s*;",
        @"(\w+)\s*\((?:\w+[\s\*&<>]*\s+)?(?:const\s+)?(\w+)[\s&]*\)\s*(?:noexcept)?\s*;",
        @"(\w+)\s*\((?:\w+[\s\*&<>]*\s+)?(\w+&&)\)\s*(?:noexcept)?\s*;",
        @"operator\s+(?:[\w:]+[\s\*&<>]*)\s*\(([^)]*)\)\s*(?:const)?\s*(?:noexcept)?\s*(?:override)?\s*;",
        @"operator\s+(?:[\w:]+[\s\*&<>]*)\s*\(\s*\)\s*(?:const)?\s*(?:noexcept)?\s*;",
      };

      foreach (var pattern in patterns)
      {
        var match = Regex.Match(candidate, pattern);
        if (match.Success)
        {
          var decl = FormatSimpleDeclaration(match, pattern)?.Trim();

          // If the declaration hasn't been added as part of another extraction method, add it
          if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl) && !declarations.Any(d => d.Trim().Contains(decl)))
          {
            declarations.Add(decl);
            break;
          }
        }
      }
    }
  }

  private static void ExtractMemberPointers(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"([\w:]+[\s\*&<>]*)\s*\((\w+)::\*\s*(\w+)\s*\)\s*\(([^)]*)\)",
      @"([\w:]+[\s\*&<>]*)\s*\((\w+)::\*\s*(\w+)\s*\)\s*const\s*\(([^)]*)\)",
      @"([\w:]+[\s\*&<>]*)\s*(\w+)::\*\s*(\w+)",
      @"template\s*<([^>]+)>\s*([\w:]+[\s\*&<>]*)\s*\((\w+)::\*\s*(\w+)\s*\)\s*\(([^)]*)\)",
    };
    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = FormatMemberPointer(match);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractTrailingReturnTypes(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"(?:constexpr|inline|static)?\s*auto\s+(\w+)\s*\(([^)]*)\)\s*->\s*([^{]+)",
      @"decltype\s*\(\s*auto\s*\)\s+(\w+)\s*\(([^)]*)\)",
      @"auto\s*\*\s*(\w+)\s*\(([^)]*)\)\s*->\s*([\w:]+[\s\*&<>]*)",
      @"auto\s*&\s*(\w+)\s*\(([^)]*)\)\s*->\s*([\w:]+[\s\*&<>]*)",
      @"auto\s*&&\s*(\w+)\s*\(([^)]*)\)\s*->\s*([\w:]+[\s\*&<>]*)",
      @"(\w+)\s*\(([^)]*)\)\s*->\s*auto(?:\s*const)?(?:\s*noexcept)?",
    };

    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = FormatTrailingReturn(match);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractFunctionPointers(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"auto\s*\(?\*\s*(\w+)\s*\)\s*\(([^)]*)\)\s*->\s*([\w:]+[\s\*&<>]*)",
      @"using\s+(\w+)\s*=\s*(?:std::)?(?:function|decltype)\s*<\s*([\w:]+[\s\*&<>]*)\s*\(([^)]*)\)\s*>",
    };
    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = FormatFunctionPointer(match, pattern);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static void ExtractTemplateFunctions(string source, ref List<string> declarations)
  {
    var patterns = new[]
    {
      @"template\s*<([^>]+)>\s*(constexpr|consteval|inline|static)?\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*\(([^)]*)\)(?:\s*const)?(?:\s*noexcept)?(?:\s*->\s*[^{]+)?(?:\s*override)?(?:\s*final)?",
      @"template\s*<([^>]+)>\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*::\s*(\w+)\s*\(([^)]*)\)",
      @"template\s*<([^>]+)>\s*requires\s+([^{]+)\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*\(([^)]*)\)",
    };

    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = FormatTemplateDeclaration(match);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static string? FormatTemplateDeclaration(Match match)
  {
    try
    {
      if (match.Groups.Count < 4) return null;

      var templateParams = match.Groups[1].Value.Trim();
      var returnType = match.Groups[^3].Value.Trim();
      var funcName = match.Groups[^2].Value.Trim();
      var args = match.Groups[^1].Value.Trim();

      if (string.IsNullOrEmpty(funcName) || funcName.Contains("::")) return null;

      return $"template <{templateParams}> {returnType} {funcName}({args});";
    }
    catch { return null; }
  }

  private static string? FormatFunctionPointer(Match match, string pattern)
  {
    try
    {
      if (match.Groups.Count < 2) return null;

      if (pattern.Contains("using"))
      {
        var alias = match.Groups[1].Value.Trim();
        var ret = match.Groups[2].Value.Trim();
        var args = match.Groups[3].Value.Trim();
        return $"using {alias} = std::function<{ret}({args})>;";
      }
      else
      {
        var returnType = match.Groups[1].Value.Trim();
        var funcName = match.Groups[2].Value.Trim();
        var args = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
        return $"{returnType} (*{funcName})({args});";
      }
    }
    catch { return null; }
  }

  private static string? FormatTrailingReturn(Match match)
  {
    try
    {
      if (match.Groups.Count < 2) return null;
      var funcName = match.Groups[1].Value.Trim();
      var args = match.Groups[2].Value.Trim();
      var returnType = (match.Groups.Count > 3 ? match.Groups[3].Value : "auto")
        .Trim()
        .TrimEnd(';')
        .Trim();

      return $"auto {funcName}({args}) -> {returnType};";
    }
    catch { return null; }
  }

  private static string? FormatMemberPointer(Match match)
  {
    try
    {
      if (match.Groups.Count < 3) return null;
      var returnType = match.Groups[1].Value.Trim();
      var className = match.Groups[2].Value.Trim();
      var funcName = match.Groups[3].Value.Trim();
      var args = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
      return $"{returnType} ({className}::{funcName})({args});";
    }
    catch { return null; }
  }

  private static string? FormatSimpleDeclaration(Match match, string pattern)
  {
    try
    {
      if (match.Groups.Count < 2) return null;
      if (pattern.Contains('=') && pattern.Contains('0'))
        return $"virtual {match.Groups[1].Value.Trim()} {match.Groups[2].Value.Trim()}({match.Groups[3].Value.Trim()}) = 0;";
      else if (pattern.Contains("virtual"))
        return $"virtual {match.Groups[1].Value.Trim()}({match.Groups[2].Value.Trim()});";
      else if (pattern.Contains("static"))
        return $"static {match.Groups[1].Value.Trim()}({match.Groups[2].Value.Trim()});";
      else if (pattern.Contains("operator"))
        return match.Value.Trim();
      else if (pattern.Contains('~'))
        return $"~{match.Groups[1].Value.Trim()}();";
      else
      {
        var ret = match.Groups[1].Value.Trim();
        var name = match.Groups[2].Value.Trim();
        var args = match.Groups[3].Value.Trim();
        if (string.IsNullOrEmpty(ret) || string.IsNullOrEmpty(name)) return null;
        return $"{ret} {name}({args});";
      }
    }
    catch { return null; }
  }

  private static string? FormatConcept(Match match)
  {
    try
    {
      if (match.Groups.Count < 3) return null;
      var templateParam = match.Groups[1].Value.Trim();
      var conceptName = match.Groups[2].Value.Trim();
      var constraint = match.Groups[^1].Value.Trim();
      return $"template <typename {templateParam}> concept {conceptName} = {constraint};";
    }
    catch { return null; }
  }


}
