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

  [GeneratedRegex(@"(?:template\s*<[^>]+>\s*)?(class|struct)\s+\w+[\w\s:,<>]*\{[\s\S]*?\};", RegexOptions.Multiline)]
  private static partial Regex ClassStructBodiesPattern();

  [GeneratedRegex(@"^\s*(?:template\s*<[^>]+>\s*)?(class|struct)\s+\w+[\w\s:,<>]*\{")]
  private static partial Regex ClassOrStructPattern();

  [GeneratedRegex(@"^((?:\[\[[^\]]*\]\]\s*)+)")]
  private static partial Regex AttributePattern();

  [GeneratedRegex(@"^\s*(constexpr|consteval|inline|static)\s+")]
  private static partial Regex QualifierPattern();

  [GeneratedRegex(@"^(?:(?:return|if|for|while|switch|case|catch|throw|delete|new|sizeof|else|do|goto|break|continue|assert)\b|(?:EXPECT|ASSERT|CHECK|REQUIRE|BENCHMARK|TEST|TYPED_TEST)_)")]
  private static partial Regex ControlFlowOrMacroPattern();

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
      // sudo needs the command it should run: `sudo -S <manager> <args>`.
      // (The pacman branch used to omit the manager entirely, producing
      // `sudo -S -S <packages>`.)
      startInfo.Arguments = $"-S {packageManager} {commandArgs}";

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
    var hppFiles = Directory.GetFiles(srcDir, "*.hpp", SearchOption.AllDirectories);

    if (cppFiles.Length == 0 && hppFiles.Length == 0)
    {
      AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] No source files found in src/. Skipping header generation.");
      return;
    }
    AnsiConsole.MarkupLine("[cyan]--- Generating library headers ---[/]");

    // Build a map of project-local header includes -> installed include path,
    // so quoted includes in the copied headers point at the full installed
    // path (e.g. "fp/result.hpp" -> "forgefp/fp/result.hpp").
    var installedIncludes = new Dictionary<string, string>();
    foreach (var hppFile in hppFiles)
    {
      var rel = Path.GetRelativePath(srcDir, hppFile).Replace('\\', '/');
      var installed = $"{projectName}/{rel}";
      installedIncludes[rel] = installed;                       // "fp/result.hpp"
      installedIncludes[Path.GetFileName(rel)] = installed;     // "result.hpp"
    }

    // Copy hand-written .hpp files from src/. An include that already resolves
    // next to the header is copied verbatim — the copy keeps the same layout,
    // so the installed tree stays byte-identical to src/ and parses standalone.
    // Includes that need the installed prefix (path-style ones like
    // "fp/result.hpp", or a name that lives in another src/ directory) are
    // rewritten so consumers can `#include <name/fp/...>`.
    foreach (var hppFile in hppFiles)
    {
      var relativePath = Path.GetRelativePath(srcDir, hppFile).Replace('\\', '/');
      var targetPath = Path.Combine(includeDir, relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

      var sourceDirectory = Path.GetDirectoryName(hppFile)!;
      var content = File.ReadAllText(hppFile);
      content = LocalIncludesPattern().Replace(content, match =>
      {
        var include = match.Groups[1].Value;

        if (File.Exists(Path.Combine(sourceDirectory, include)))
          return match.Value;

        return installedIncludes.TryGetValue(include, out var installed)
          ? $"#include \"{installed}\""
          : match.Value;
      });
      File.WriteAllText(targetPath, content);

      AnsiConsole.MarkupLine($"[green]Copied:[/] {targetPath}");
    }

    foreach (var cppFile in cppFiles)
    {
      var fileName = Path.GetFileNameWithoutExtension(cppFile);
      var headerFileName = $"{fileName}.hpp";
      var headerPath = Path.Combine(includeDir, headerFileName);
      var content = File.ReadAllText(cppFile);

      // Skip if a hand-written header exists (from src/ or manual in include/)
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
    AnsiConsole.MarkupLine($"[bold green]Header generation complete. Found {cppFiles.Length + hppFiles.Length} source file(s).[/]");
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

      // Handle template classes/structs where template <...> is on a separate line
      if (!isClassOrStruct && trimmed.StartsWith("template"))
      {
        int j = i + 1;
        while (j < lines.Length && j < i + 10) // Template headers can be long
        {
          var peekTrimmed = lines[j].Trim();
          if (ClassOrStructPattern().IsMatch(peekTrimmed))
          {
            isClassOrStruct = true;
            break;
          }
          if (peekTrimmed.Contains(';') || (peekTrimmed.Contains('(') && !peekTrimmed.Contains('<'))) break;
          j++;
        }
      }

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

      // Skip control-flow statements and test/macro invocations that only look like calls
      if (ControlFlowOrMacroPattern().IsMatch(trimmed)) continue;

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
          var fullLine = GetFullLine(source, match);
          if (fullLine.StartsWith("template")) continue;
        }

        if (!decl.Contains("_impl") && !decl.Contains("detail::"))
          declarations.Add(decl);
      }
    }
  }

  private static string GetFullLine(string source, Match match)
  {
    var lineStart = source.LastIndexOf('\n', match.Index) + 1;
    var endSearchStart = Math.Min(match.Index + match.Length, source.Length - 1);
    var lineEnd = source.IndexOf('\n', endSearchStart);
    return source[lineStart..(lineEnd < 0 ? source.Length : lineEnd)].TrimStart();
  }

  private static bool HasTemplatePrecededLine(string source, int matchIndex)
  {
    var cursor = matchIndex;
    while (cursor >= 0)
    {
      // Start of the line containing (or starting at) cursor
      var lineStart = source.LastIndexOf('\n', cursor) + 1;
      // Index of the '\n' that ends the previous line, or -1
      var prevEnd = lineStart - 1;
      if (prevEnd < 0) return false;

      var prevStart = source.LastIndexOf('\n', prevEnd - 1) + 1;
      var prevLine = source[prevStart..prevEnd].Trim();
      if (prevLine.Length == 0)
      {
        cursor = prevStart - 1;
        continue;
      }
      return prevLine.StartsWith("template")
        || prevLine.StartsWith("requires")
        || prevLine.StartsWith("concept");
    }
    return false;
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
    var prevDeclaredTemplate = false;

    foreach (var line in lines)
    {
      // Skip template functions - handled separately 
      if (line.TrimStart().StartsWith("template"))
      {
        prevDeclaredTemplate = true;
        continue;
      }
      if (line.TrimStart().StartsWith("requires") || line.TrimStart().StartsWith("concept"))
      {
        prevDeclaredTemplate = true;
        continue;
      }

      var trimmed = line.Trim();
      if (string.IsNullOrEmpty(trimmed))
      {
        prevDeclaredTemplate = false;
        continue;
      }

      // A signature following a template/requires/concept header belongs to that declaration -
      // handled separately by ExtractTemplateFunctions / ExtractConcepts
      var isTemplateBody = prevDeclaredTemplate;
      prevDeclaredTemplate = prevDeclaredTemplate && !trimmed.EndsWith(';') && trimmed.EndsWith(',');
      if (isTemplateBody || trimmed.EndsWith(">")) continue;

      // Skip trailing-return functions - handled separately by ExtractTrailingReturnTypes
      if (trimmed.Contains("->")) continue;

      // Skip control-flow statements and test/macro invocations that only look like calls
      if (ControlFlowOrMacroPattern().IsMatch(trimmed)) continue;

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
        candidate = trimmed.TrimEnd(';');
      }
      else if (trimmed.Contains('(') && (trimmed.Contains('{') || trimmed.EndsWith(')')))
      {
        // Try to capture the signature part before the body or newline
        int braceIndex = trimmed.IndexOf('{');
        candidate = (braceIndex >= 0 ? trimmed[..braceIndex] : trimmed).Trim();
        candidate = candidate.TrimEnd(';');
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
        // Anchor to the full line so body statements (return f(x);, o && pred(x))
        // can never match a partial signature
        var match = Regex.Match(candidate, $"^{pattern}$");
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
        var fullLine = GetFullLine(source, match);

        if (fullLine.StartsWith("template")
            || fullLine.StartsWith("requires")
            || fullLine.StartsWith("concept")) continue;
        // Template-parameterized functions are handled by ExtractTemplateFunctions,
        // which emits them with their `template <...>` prefix and trailing return type
        if (HasTemplatePrecededLine(source, match.Index)) continue;
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
        @"template\s*<(?<tparams>[^>]+)>\s*(?:requires\s+[\w:]+(?:<[^>]*>)?\s+)?(?:constexpr|consteval|inline|static)?\s*(?<ret>[\w:]+(?:\s*<[^>]*>)?[\s\*&<>]*)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)(?:\s*const)?(?:\s*noexcept)?(?<arrow>\s*->\s*[^{;]+)?(?:\s*override)?(?:\s*final)?",
        @"template\s*<([^>]+)>\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*::\s*(\w+)\s*\(([^)]*)\)",
        @"template\s*<([^>]+)>\s*requires\s+([^{]+)\s*([\w:]+[\s\*&<>]*)\s+(\w+)\s*\(([^)]*)\)",
    };

    for (var i = 0; i < patterns.Length; i++)
    {
      foreach (Match match in Regex.Matches(normalised, patterns[i], RegexOptions.Multiline))
      {
        var decl = FormatTemplateDeclaration(match, i);
        if (!string.IsNullOrEmpty(decl) && !declarations.Contains(decl))
          declarations.Add(decl);
      }
    }
  }

  private static string? FormatTemplateDeclaration(Match match, int patternIndex)
  {
    try
    {
      string templateParams;
      string returnType;
      string funcName;
      string args;
      var arrowSuffix = "";

      if (patternIndex == 0)
      {
        templateParams = match.Groups["tparams"].Value.Trim();
        returnType = match.Groups["ret"].Value.Trim();
        funcName = match.Groups["name"].Value.Trim();
        args = match.Groups["args"].Value.Trim();
        if (match.Groups["arrow"].Success)
          arrowSuffix = match.Groups["arrow"].Value.TrimEnd();
      }
      else
      {
        if (match.Groups.Count < 4) return null;
        templateParams = match.Groups[1].Value.Trim();
        returnType = match.Groups[^3].Value.Trim();
        funcName = match.Groups[^2].Value.Trim();
        args = match.Groups[^1].Value.Trim();
      }

      if (string.IsNullOrEmpty(funcName) || funcName.Contains("::")) return null;

      // Strip any trailing `requires ...` leaked into templateParams
      var reqIdx = templateParams.IndexOf("requires", StringComparison.Ordinal);
      if (reqIdx >= 0) templateParams = templateParams[..reqIdx].Trim().TrimEnd(',').Trim();

      // Strip qualifier keywords that may bleed into returnType
      returnType = QualifierPattern().Replace(returnType, "").Trim();

      return $"template <{templateParams}> {returnType} {funcName}({args}){arrowSuffix};";
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
