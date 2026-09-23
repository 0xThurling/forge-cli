using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Spectre.Console;

namespace forge;

/// <summary>
/// A shared cache of fetched git dependencies, keyed by repository and ref, so
/// projects (and CI runs) do not clone the same dependency over and over.
/// </summary>
/// <remarks>
/// The cache is handed to CMake as <c>FETCHCONTENT_SOURCE_DIR_&lt;NAME&gt;</c>,
/// which makes FetchContent skip the download entirely. Only immutable
/// references are cached — a locked commit, or a version-like tag — because a
/// branch ("main") moves and a cached copy would freeze it.
/// </remarks>
internal static class DependencyCache
{
  /// <summary>Whether the cache is enabled for this build.</summary>
  public static bool Enabled(string configured) =>
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORGE_NO_CACHE")) &&
    !string.Equals(configured.Trim(), "off", StringComparison.OrdinalIgnoreCase);

  /// <summary>The cache directory: $FORGE_CACHE_DIR, else ~/.cache/forge/deps.</summary>
  public static string Root()
  {
    var configured = Environment.GetEnvironmentVariable("FORGE_CACHE_DIR");
    if (!string.IsNullOrWhiteSpace(configured))
      return Path.GetFullPath(configured);

    var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
    if (!string.IsNullOrWhiteSpace(xdg))
      return Path.Combine(xdg, "forge", "deps");

    return Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "forge", "deps");
  }

  /// <summary>
  /// Whether a dependency may be cached: it needs an immutable reference — a
  /// locked commit, a version-like tag, or a tag with a dotted version in it
  /// (<c>release-2.32.10</c>). A branch has neither, and caching one would
  /// freeze a moving reference.
  /// </summary>
  public static bool IsCacheable(string? lockedCommit, string tag) =>
    !string.IsNullOrWhiteSpace(lockedCommit) ||
    GitTags.IsVersionLike(tag) ||
    (tag.Contains('.') && tag.Any(char.IsDigit));

  /// <summary>
  /// The CMake line that points FetchContent at a cached checkout, or an empty
  /// string when the cache is off or the entry could not be prepared.
  /// </summary>
  public static string CMakeVariableFor(
    string name, string repository, string tag, string? lockedCommit)
  {
    var cached = Ensure(name, repository, tag, lockedCommit);
    return cached is null
      ? string.Empty
      : $"set(FETCHCONTENT_SOURCE_DIR_{name.ToUpperInvariant()} \"{cached.Replace('\\', '/')}\" CACHE PATH \"\" FORCE)";
  }

  /// <summary>The cache entry for a repository at a reference.</summary>
  public static string PathFor(string name, string repository, string reference)
  {
    var key = Convert.ToHexString(
      SHA256.HashData(Encoding.UTF8.GetBytes($"{repository}\n{reference}")))[..8].ToLowerInvariant();

    var safeName = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());
    return Path.Combine(Root(), $"{safeName}-{key}");
  }

  /// <summary>
  /// The cache entry for a dependency, cloned when it is missing. Returns null
  /// when the cache is off, the dependency is generated, or the clone failed
  /// (the caller then lets FetchContent do its normal — slower — thing).
  /// </summary>
  public static string? Ensure(string name, string repository, string tag, string? lockedCommit)
  {
    if (!IsCacheable(lockedCommit, tag))
      return null;

    var reference = string.IsNullOrWhiteSpace(lockedCommit) ? tag : lockedCommit!;
    var path = PathFor(name, repository, reference);
    if (Directory.Exists(path))
      return path;

    Directory.CreateDirectory(Root());
    try
    {
      // Clone by the tag (always fetchable), then check out the locked commit
      // when the lock points somewhere else.
      if (Run("git", ["clone", "--quiet", "--depth", "1", "--branch", tag, repository, path]) != 0 ||
          (!string.IsNullOrWhiteSpace(lockedCommit) &&
           Run("git", ["-C", path, "checkout", "--quiet", lockedCommit!]) != 0))
      {
        Cleanup(path);
        AnsiConsole.MarkupLine(
          $"[dim]Dependency cache: could not cache {name} ({tag}); fetching it per project instead.[/]");
        return null;
      }

      return path;
    }
    catch (Exception)
    {
      Cleanup(path);
      return null;
    }
  }

  /// <summary>Every cache entry, with its size on disk.</summary>
  public static List<(string Name, string Path, long Bytes)> List()
  {
    var root = Root();
    if (!Directory.Exists(root))
      return [];

    return Directory.EnumerateDirectories(root)
      .OrderBy(path => path, StringComparer.Ordinal)
      .Select(path => (Path.GetFileName(path), path, Size(path)))
      .ToList();
  }

  /// <summary>Removes one entry, or every entry when the name is null.</summary>
  public static int Clear(string? name)
  {
    var removed = 0;
    foreach (var (entryName, path, _) in List())
    {
      if (!string.IsNullOrWhiteSpace(name) &&
          !entryName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      Cleanup(path);
      removed++;
    }

    return removed;
  }

  private static long Size(string directory)
  {
    try
    {
      return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Sum(file => new FileInfo(file).Length);
    }
    catch (Exception)
    {
      return 0;
    }
  }

  private static void Cleanup(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, recursive: true);
    }
    catch (Exception)
    {
      // A partial entry is harmless: the next build re-clones it.
    }
  }

  private static int Run(string fileName, IReadOnlyList<string> arguments)
  {
    var startInfo = new ProcessStartInfo(fileName)
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true,
    };
    foreach (var argument in arguments)
      startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo);
    if (process == null)
      return 1;

    process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode;
  }
}
