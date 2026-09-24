namespace forge;

/// <summary>
/// A snapshot of the files under some directories, with their write times.
/// </summary>
/// <remarks>
/// Shared by <c>forge watch</c> and <c>forge hot</c>: the former rebuilds on
/// any change, the latter reloads the running process and only rebuilds when
/// the set of files changed (the generated compile commands depend on it).
/// </remarks>
public static class FileSnapshot
{
  /// <summary>Every file under the given directories, with its write time.</summary>
  public static Dictionary<string, DateTime> Take(IEnumerable<string> directories)
  {
    var files = new Dictionary<string, DateTime>(StringComparer.Ordinal);
    foreach (var directory in directories)
    {
      foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
      {
        var parts = file.Split(Path.DirectorySeparatorChar);
        if (parts.Any(part => part is "build" or ".git" || part.StartsWith("build-")))
          continue;

        files[file] = File.GetLastWriteTimeUtc(file);
      }
    }

    return files;
  }

  /// <summary>What changed between two snapshots.</summary>
  public static FileChanges Diff(
    Dictionary<string, DateTime> before, Dictionary<string, DateTime> after)
  {
    var added = new List<string>();
    var modified = new List<string>();
    var removed = new List<string>();

    foreach (var (path, stamp) in after)
    {
      if (!before.TryGetValue(path, out var previous))
        added.Add(path);
      else if (previous != stamp)
        modified.Add(path);
    }

    foreach (var path in before.Keys)
    {
      if (!after.ContainsKey(path))
        removed.Add(path);
    }

    return new FileChanges(added, removed, modified);
  }
}

/// <summary>The paths that were added, removed or rewritten between snapshots.</summary>
public sealed record FileChanges(
  IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Modified)
{
  /// <summary>Every changed path.</summary>
  public IEnumerable<string> Paths => Modified.Concat(Added).Concat(Removed);

  /// <summary>How many paths changed.</summary>
  public int Count => Added.Count + Removed.Count + Modified.Count;

  /// <summary>
  /// True when a file appeared or disappeared, which changes the generated
  /// compile commands and needs a regeneration, not just a reload.
  /// </summary>
  public bool IsStructural => Added.Count > 0 || Removed.Count > 0;
}
