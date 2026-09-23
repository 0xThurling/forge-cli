namespace forge.Models
{
  /// <summary>
  /// A git dependency resolved to an exact commit.
  /// </summary>
  public class LockedDependency
  {
    /// <summary>Repository URL, as declared.</summary>
    public string Git { get; set; } = string.Empty;

    /// <summary>The declared tag/branch, recorded so a changed ref re-resolves.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>The commit SHA the ref resolved to.</summary>
    public string Commit { get; set; } = string.Empty;
  }

  /// <summary>
  /// <c>forge.lock</c>: the exact revisions a build should use.
  /// </summary>
  /// <remarks>
  /// Git dependencies are declared with a tag or branch, which can move. The
  /// lock records the commit each ref resolved to at <c>forge install</c> time,
  /// and the generated CMake fetches that commit instead — so two machines (or
  /// two CI runs) build the same sources. Commit the file; delete an entry (or
  /// run <c>forge install --update</c>) to move on.
  /// </remarks>
  public class Lockfile
  {
    /// <summary>Format version, so the shape can change later.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Locked git dependencies, keyed by the name in forge.lua.</summary>
    public Dictionary<string, LockedDependency> Git { get; set; } = new();
  }
}
