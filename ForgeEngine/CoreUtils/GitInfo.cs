using System.Diagnostics;

namespace forge.ForgeEngine.CoreUtils;

/// <summary>
/// Reads the Git state of a directory. Used by the <c>forge.git.*</c> Lua API
/// and by versioning, so both report the same values.
/// </summary>
/// <remarks>
/// Every method returns null (or false) when git is missing, the directory is
/// not a repository, or the command fails — scripts should treat that as "no
/// information" rather than an error.
/// </remarks>
public static class GitInfo
{
  /// <summary>The most recent tag reachable from HEAD, with distance and dirty flag.</summary>
  public static string? Describe(string directory) =>
    Run(directory, "describe", "--tags", "--always", "--dirty");

  /// <summary>The abbreviated commit hash.</summary>
  public static string? Rev(string directory) =>
    Run(directory, "rev-parse", "--short", "HEAD");

  /// <summary>The exact tag at HEAD, or null when HEAD is not tagged.</summary>
  public static string? Tag(string directory) =>
    Run(directory, "describe", "--tags", "--exact-match");

  /// <summary>The current branch name (or "HEAD" when detached).</summary>
  public static string? Branch(string directory) =>
    Run(directory, "rev-parse", "--abbrev-ref", "HEAD");

  /// <summary>Whether the working tree has uncommitted changes.</summary>
  public static bool Dirty(string directory) =>
    Run(directory, "status", "--porcelain") is { Length: > 0 };

  /// <summary>
  /// A semantic version derived from the newest tag, e.g. <c>1.4.2</c>, plus the
  /// number of commits since it. Null when the repository has no version-like tag.
  /// </summary>
  public static (string Version, int CommitsSince) DescribeVersion(string directory)
  {
    var description = Run(directory, "describe", "--tags", "--long", "--match", "v[0-9]*");
    if (string.IsNullOrWhiteSpace(description))
      return (string.Empty, 0);

    // v1.4.2-5-gabc1234
    var parts = description.Split('-');
    if (parts.Length < 3)
      return (description.TrimStart('v'), 0);

    var commits = int.TryParse(parts[^2], out var parsed) ? parsed : 0;
    return (parts[0].TrimStart('v'), commits);
  }

  private static string? Run(string directory, params string[] arguments)
  {
    try
    {
      var startInfo = new ProcessStartInfo("git")
      {
        WorkingDirectory = directory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

      using var process = Process.Start(startInfo);
      if (process == null)
        return null;

      var output = process.StandardOutput.ReadToEnd().Trim();
      process.StandardError.ReadToEnd();
      process.WaitForExit();
      return process.ExitCode == 0 ? output : null;
    }
    catch (Exception)
    {
      return null; // git missing, not a repository, …
    }
  }
}
