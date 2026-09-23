using Spectre.Console;

namespace forge;

/// <summary>
/// Project-local templates for <c>forge new</c>.
/// </summary>
/// <remarks>
/// A template lives in <c>.config/forge/templates/&lt;kind&gt;.&lt;ext&gt;</c>
/// (for example <c>class.hpp</c>, <c>struct.cpp</c>) and is rendered with
/// <c>{{name}}</c> and <c>{{NAME}}</c> replaced. Without a template the built-in
/// scaffolding is used, so projects opt in simply by adding a file.
/// </remarks>
public static class TemplateManager
{
  public const string Directory = ".config/forge/templates";

  /// <summary>
  /// Renders the project's template for <paramref name="kind"/> with the given
  /// extension, or null when the project has none.
  /// </summary>
  public static string? Render(string kind, string name, string extension)
  {
    if (!System.IO.Directory.Exists(Directory))
      return null;

    // Projects name headers .h or .hpp; accept either for the same kind.
    var alternatives = extension switch
    {
      ".h" => new[] { ".h", ".hpp" },
      ".hpp" => new[] { ".hpp", ".h" },
      _ => new[] { extension }
    };

    foreach (var candidate in alternatives.Select(ext => $"{kind}{ext}")
               .Append($"{kind}.template"))
    {
      var path = Path.Combine(Directory, candidate);
      if (!File.Exists(path))
        continue;

      var content = File.ReadAllText(path);
      return content
        .Replace("{{name}}", name)
        .Replace("{{NAME}}", name.ToUpperInvariant());
    }

    return null;
  }

  /// <summary>True when the project provides at least one template.</summary>
  public static bool HasAny() =>
    System.IO.Directory.Exists(Directory) &&
    System.IO.Directory.GetFiles(Directory).Length > 0;

  /// <summary>Lists the template files, for diagnostics.</summary>
  public static IEnumerable<string> Names() =>
    System.IO.Directory.Exists(Directory)
      ? System.IO.Directory.GetFiles(Directory).Select(Path.GetFileName).OrderBy(n => n)!
      : [];
}
