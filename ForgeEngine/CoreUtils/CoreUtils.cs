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

  [GeneratedRegex(@"(class|struct)\s+\w+[\w\s:,<>]*\{[\s\S]*?\};", RegexOptions.Multiline)]
  private static partial Regex ClassStructBodiesPattern();

  [GeneratedRegex(@"^(class|struct)\s+\w+[\w\s:,<>]*\{")]
  private static partial Regex ClassOrStructPattern();

  [GeneratedRegex(@"^((?:\[\[[^\]]*\]\]\s*)+)")]
  private static partial Regex AttributePattern();

  [GeneratedRegex(@"^\s*(constexpr|consteval|inline|static)\s+")]
  private static partial Regex QualifierPattern();
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

  public static void GenerateLibraryHeaders(string projectName)
  {
    var srcDir = "src";
    var includeDir = Path.Combine("include", projectName);
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

    ExtractClassBodies(source, ref declarations);
    var sourceWithoutClasses = ClassStructBodiesPattern().Replace(source, "");

    // Extract various complex declarations
    ExtractTemplateFunctions(sourceWithoutClasses, ref declarations);
    ExtractFunctionPointers(sourceWithoutClasses, ref declarations);
    ExtractFunctionsWithPointerParams(sourceWithoutClasses, ref declarations);
    ExtractTrailingReturnTypes(sourceWithoutClasses, ref declarations);
    ExtractMemberPointers(sourceWithoutClasses, ref declarations);
    ExtractSimpleFunctions(sourceWithoutClasses, ref declarations);
    ExtractConcepts(sourceWithoutClasses, ref declarations);
    ExtractOperators(sourceWithoutClasses, ref declarations);
    ExtractTypeAliases(sourceWithoutClasses, ref declarations);

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

  private static void ExtractClassBodies(string source, ref List<string> declarations)
  {
    var lines = source.Split('\n');
    int i = 0;

    while (i < lines.Length)
    {
      var trimmed = lines[i].Trim();

      // Match: class Foo { or struct Foo { or class Foo : public Bar {
      bool isClassOrStruct = ClassOrStructPattern().IsMatch(trimmed);

      if (isClassOrStruct)
      {
        var block = new StringBuilder();
        int depth = 0;

        // Collect lines until braces balance and block ends with };
        while (i < lines.Length)
        {
          var line = lines[i];
          block.AppendLine(line);

          foreach (var c in line)
          {
            if (c == '{') depth++;
            else if (c == '}') depth--;
          }

          i++;

          // Block is complete when all braces are closed
          if (depth == 0 && block.ToString().TrimEnd().EndsWith("};")) break;
        }

        var classDecl = block.ToString().TrimEnd();
        if (!declarations.Contains(classDecl)) declarations.Add(classDecl);
      }
      else
      {
        i++;
      }
    }
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

      if (trimmed.StartsWith("requires")) continue;

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
        @"template\s*<\s*(?:typename\s+\w+\s*,\s*)+typename\s+\w+\s*>\s*using\s+\w+\s*=\s*[^;]+;",
        @"template\s*<\s*typename\s+\w+\s*>\s*using\s+\w+\s*=\s*[^;]+;",
        @"using\s+(\w+)\s*=\s*(?:typename\s+)?([^{;]+);",
        @"using\s+enum\s+(\w+)\s*;",
        @"using\s+(\w+)::(\w+)\s*;",
    };

    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
      {
        var decl = match.Value.Trim();

        // Skip if already captured as a template alias
        if (declarations.Contains(decl)) continue;

        // For the simple alias pattern, skip lines that are part of a template
        if (!pattern.Contains("template") && !pattern.Contains("enum") && !pattern.Contains("::"))
        {
          var lineStart = source.LastIndexOf('\n', match.Index) + 1;
          var lineEnd = source.IndexOf('\n', match.Index);
          var fullLine = source[lineStart..(lineEnd < 0 ? source.Length : lineEnd)].TrimStart();
          if (fullLine.StartsWith("template")) continue;
        }

        if (!decl.Contains("_impl") && !decl.Contains("detail::"))
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
    var lines = source.Split('\n');
    var i = 0;
    while (i < lines.Length)
    {
      var trimmed = lines[i].Trim();

      bool startsTemplate = trimmed.StartsWith("template");
      bool nextIsConcept = startsTemplate && i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("concept");
      bool inlineConcept = startsTemplate && trimmed.Contains("concept");

      if (nextIsConcept || inlineConcept)
      {
        var block = new StringBuilder();
        int depth = 0;

        while (i < lines.Length)
        {
          var line = lines[i].Trim();
          if (block.Length > 0) block.Append(' ');
          block.Append(line);
          i++;

          foreach (var c in line)
          {
            if (c == '{') depth++;
            else if (c == '}') depth--;
          }

          // Stop when all braces are balanced and line ends with ;
          if (depth == 0 && block.ToString().Trim().EndsWith(';'))
            break;
        }

        var decl = block.ToString().Trim();
        if (decl.Contains("concept") && !declarations.Contains(decl))
          declarations.Add(decl);
        continue;
      }
      i++;
    }
  }

  private static void ExtractSimpleFunctions(string source, ref List<string> declarations)
  {
    var lines = source.Split('\n');

    foreach (var line in lines)
    {
      // Skip template functions - handled separately 
      if (line.TrimStart().StartsWith("template")) continue;
      if (line.TrimStart().StartsWith("requires")) continue;
      if (line.TrimStart().StartsWith("concept")) continue;

      var trimmed = line.Trim();
      if (string.IsNullOrEmpty(trimmed)) continue;

      var attributePrefix = "";
      var attributeMatch = AttributePattern().Match(trimmed);

      if (attributeMatch.Success)
      {
        attributePrefix = attributeMatch.Value;
        trimmed = trimmed[attributePrefix.Length..].TrimStart();
      }

      string? candidate = null;
      if (trimmed.EndsWith(';'))
      {
        candidate = trimmed;
      }
      else if (trimmed.Contains('(') && (trimmed.Contains('{') || trimmed.EndsWith(')')))
      {
        // Try to capture the signature part before the body or newline
        int braceIndex = trimmed.IndexOf('{');
        if (braceIndex >= 0)
        {
          candidate = trimmed[..braceIndex].Trim();
        }
        else
        {
          candidate = trimmed;
        }

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
        @"(?<virt>virtual\s+)?(?<stat>static|explicit|inline|constexpr|consteval)?\s*(?<ret>[\w:*&<>\[\]]+[\s*&]*)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)(?<cq>\s*const)?(?:\s*noexcept)?(?:\s*(?<pure>=\s*0))?",
        // pure-virtual shorthand
        @"(?<virt>virtual)\s+(?<ret>[\w:*&<>]+[\s*&]*)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)(?<cq>\s*const)?(?:\s*noexcept)?(?:\s*(?<pure>=\s*0))?",
      };

      foreach (var pattern in patterns)
      {
        var match = Regex.Match(candidate, pattern);
        if (match.Success)
        {
          var decl = FormatSimpleDeclaration(match, pattern)?.Trim();

          if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          {
            decl = attributePrefix + decl;
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
        var lineStart = source.LastIndexOf('\n', match.Index) + 1;
        var lineEnd = source.IndexOf('\n', match.Index);
        var fullLine = source[lineStart..(lineEnd < 0 ? source.Length : lineEnd)].TrimStart();

        if (fullLine.StartsWith("requires") || fullLine.StartsWith("concept")) continue;
        if (pattern.Contains("->\\s*auto") && !fullLine.Contains("->")) continue;

        // Extract attribute prefix from the full line
        var attributePrefix = "";
        var attrMatch = AttributePattern().Match(fullLine);
        if (attrMatch.Success)
          attributePrefix = attrMatch.Value;

        var decl = FormatTrailingReturn(match)?.Trim();
        if (!string.IsNullOrEmpty(decl))
        {
          decl = attributePrefix + decl;
          if (!declarations.Contains(decl))
            declarations.Add(decl);
        }
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
    // Collapse multi-line template+requires+declaration into single lines
    var normalised = Regex.Replace(
        source,
        @"(template\s*<[^>]+>)\s*\n\s*(requires\s+[^\n]+)\s*\n\s*([^\n;{]+;)",
        "$1 $2 $3",
        RegexOptions.Multiline
    );

    // Collapse template + declaration split across two lines (no requires)
    normalised = Regex.Replace(
        normalised,
        @"(template\s*<[^>]+>)\s*\n\s*([^\n;{]+;)",
        "$1 $2",
        RegexOptions.Multiline
    );

    var patterns = new[]
    {
        @"template\s*<([^>]+)>\s*(?:requires\s+[\w:]+(?:<[^>]*>)?\s+)?(?:constexpr|consteval|inline|static)?\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*\(([^)]*)\)(?:\s*const)?(?:\s*noexcept)?(?:\s*->\s*[^{;]+)?(?:\s*override)?(?:\s*final)?",
        @"template\s*<([^>]+)>\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*::\s*(\w+)\s*\(([^)]*)\)",
        @"template\s*<([^>]+)>\s*requires\s+([^{]+)\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*\(([^)]*)\)",
    };

    foreach (var pattern in patterns)
    {
      foreach (Match match in Regex.Matches(normalised, pattern, RegexOptions.Multiline))
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

      // Strip any trailing `requires ...` leaked into templateParams
      var reqIdx = templateParams.IndexOf("requires", StringComparison.Ordinal);
      if (reqIdx >= 0) templateParams = templateParams[..reqIdx].Trim().TrimEnd(',').Trim();

      // Strip qualifier keywords that may bleed into returnType
      returnType = QualifierPattern().Replace(returnType, "").Trim();

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

      // Group layout for the main pattern:
      // G1 = virtual (optional), G2 = static/explicit/inline/constexpr (optional),
      // G3 = return type, G4 = function name, G5 = args, G6 = const (optional)
      // But since patterns vary, use named groups instead:

      var isVirtual = match.Groups["virt"].Success && !string.IsNullOrWhiteSpace(match.Groups["virt"].Value);
      var isStatic = match.Groups["stat"].Success && !string.IsNullOrWhiteSpace(match.Groups["stat"].Value);
      var isPureVirt = match.Groups["pure"].Success && !string.IsNullOrWhiteSpace(match.Groups["pure"].Value);
      var ret = match.Groups["ret"].Value.Trim();
      var name = match.Groups["name"].Value.Trim();
      var args = match.Groups["args"].Value.Trim();
      var constQual = match.Groups["cq"].Success ? match.Groups["cq"].Value.Trim() : "";
      var suffix = string.IsNullOrEmpty(constQual) ? "" : " const";

      if (string.IsNullOrEmpty(ret) || string.IsNullOrEmpty(name)) return null;

      string prefix = isVirtual ? "virtual " : isStatic ? "static " : "";
      string pure = isPureVirt ? " = 0" : "";

      return $"{prefix}{ret} {name}({args}){suffix}{pure};";
    }
    catch { return null; }
  }
}
