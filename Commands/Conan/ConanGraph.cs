using System.Diagnostics;
using System.Text.Json;

namespace forge.Commands.Conan;

/// <summary>One package in Conan's resolved graph.</summary>
/// <remarks>
/// <see cref="Id"/> is the node key in the graph ("0", "1", …): the edges
/// (<c>requires</c>, <c>dependencies</c>) refer to nodes by id, not by name,
/// which is why the id has to survive parsing.
/// </remarks>
internal sealed record ConanPackage(
  string Id,
  string Name,
  string Version,
  string Reference,
  List<string> Requires,
  List<string> RequiredBy);

/// <summary>
/// Reads the resolved dependency graph from <c>conan graph info</c>, which is
/// what <c>forge why</c> needs to explain where a transitive package comes from.
/// </summary>
internal static class ConanGraph
{
  /// <summary>
  /// The resolved graph for a conanfile, or null when Conan is not installed,
  /// the file does not exist, or the command failed (the caller falls back to
  /// what <c>forge.lua</c> declares).
  /// </summary>
  public static async Task<List<ConanPackage>?> LoadAsync(string conanfilePath)
  {
    if (!File.Exists(conanfilePath))
      return null;

    try
    {
      var startInfo = new ProcessStartInfo("conan")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      startInfo.ArgumentList.Add("graph");
      startInfo.ArgumentList.Add("info");
      startInfo.ArgumentList.Add(conanfilePath);
      startInfo.ArgumentList.Add("--format=json");

      using var process = Process.Start(startInfo);
      if (process == null)
        return null;

      var output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();
      if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        return null;

      return Parse(output);
    }
    catch (Exception)
    {
      return null; // conan missing
    }
  }

  /// <summary>Parses <c>graph.nodes</c> into packages, tolerating both shapes.</summary>
  internal static List<ConanPackage> Parse(string json)
  {
    var packages = new List<ConanPackage>();
    using var document = JsonDocument.Parse(json);
    if (!document.RootElement.TryGetProperty("graph", out var graph) ||
        !graph.TryGetProperty("nodes", out var nodes))
    {
      return packages;
    }

    foreach (var node in nodes.EnumerateObject())
    {
      var value = node.Value;
      var reference = value.TryGetProperty("ref", out var refProperty) && refProperty.ValueKind == JsonValueKind.String
        ? refProperty.GetString() ?? string.Empty
        : string.Empty;

      var requires = ReadIds(value, "requires");
      var requiredBy = ReadIds(value, "dependencies");
      var (name, version) = SplitReference(reference);

      packages.Add(new ConanPackage(node.Name, name, version, reference, requires, requiredBy));
    }

    return packages;
  }

  /// <summary>Finds a package by name (case-insensitive), skipping the consumer node.</summary>
  public static ConanPackage? Find(List<ConanPackage> packages, string name) =>
    packages.FirstOrDefault(package =>
      !string.IsNullOrEmpty(package.Name) &&
      string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase));

  /// <summary>
  /// The chain of packages that pull <paramref name="package"/> in, from the
  /// package itself up to the direct dependencies (breadth-first, so the
  /// shortest explanation wins).
  /// </summary>
  public static List<ConanPackage> Explain(List<ConanPackage> packages, ConanPackage package)
  {
    // Edges carry node ids; fall back to matching by reference or name for
    // graph shapes that inline them instead.
    var byId = packages.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

    var chain = new List<ConanPackage> { package };
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { package.Name };
    var current = package;

    // The graph lists ids, not names; match an id to a package by position when
    // the id set is numeric (the usual Conan shape) and by reference otherwise.
    while (true)
    {
      ConanPackage? parent = null;
      foreach (var id in current.RequiredBy)
      {
        var candidate = byId.TryGetValue(id, out var byNodeId)
          ? byNodeId
          : packages.FirstOrDefault(item =>
              item.Reference == id || item.Name == id || item.Name == SplitReference(id).Name);

        // The consumer node has no name: it is the project itself.
        if (candidate is not null && !string.IsNullOrEmpty(candidate.Name) && !seen.Contains(candidate.Name))
        {
          parent = candidate;
          break;
        }
      }

      if (parent is null)
        break;

      seen.Add(parent.Name);
      chain.Add(parent);
      current = parent;

      if (chain.Count > 16)
        break; // a cycle or a very deep chain: stop explaining
    }

    return chain;
  }

  private static (string Name, string Version) SplitReference(string reference)
  {
    if (string.IsNullOrEmpty(reference))
      return (string.Empty, string.Empty);

    var withoutRevision = reference.Split('#')[0];
    var separator = withoutRevision.IndexOf('/');
    return separator < 0
      ? (withoutRevision, string.Empty)
      : (withoutRevision[..separator], withoutRevision[(separator + 1)..]);
  }

  /// <summary>
  /// Reads a node's id list. Conan writes ids as strings in <c>requires</c> and
  /// as an object in <c>dependencies</c>; both are accepted.
  /// </summary>
  private static List<string> ReadIds(JsonElement node, string property)
  {
    var ids = new List<string>();
    if (!node.TryGetProperty(property, out var value))
      return ids;

    switch (value.ValueKind)
    {
      case JsonValueKind.Array:
        foreach (var element in value.EnumerateArray())
        {
          if (element.ValueKind == JsonValueKind.String)
            ids.Add(element.GetString() ?? string.Empty);
          else if (element.ValueKind == JsonValueKind.Object &&
                   element.TryGetProperty("ref", out var elementRef) &&
                   elementRef.ValueKind == JsonValueKind.String)
            ids.Add(elementRef.GetString() ?? string.Empty);
        }
        break;

      case JsonValueKind.Object:
        foreach (var property2 in value.EnumerateObject())
          ids.Add(property2.Name);
        break;
    }

    return ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
  }
}
