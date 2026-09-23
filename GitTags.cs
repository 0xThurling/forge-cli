using System.Diagnostics;

namespace forge;

/// <summary>
/// Finds the newest version-like tag of a Git repository, over the network
/// (<c>git ls-remote</c>). Shared by <c>forge outdated</c> and
/// <c>forge upgrade</c> so both agree on what "newest" means.
/// </summary>
internal static class GitTags
{
  /// <summary>The newest semver-like tag of a repository, or null.</summary>
  public static async Task<string?> LatestVersionTagAsync(string repository)
  {
    try
    {
      var psi = new ProcessStartInfo("git")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      psi.ArgumentList.Add("ls-remote");
      psi.ArgumentList.Add("--tags");
      psi.ArgumentList.Add(repository);

      using var process = Process.Start(psi);
      if (process == null)
        return null;

      var output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();
      if (process.ExitCode != 0)
        return null;

      var tags = output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('\t'))
        .Where(parts => parts.Length == 2 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal))
        .Select(parts => parts[1]["refs/tags/".Length..].Trim())
        .Where(tag => !tag.EndsWith("^{}", StringComparison.Ordinal))
        .Where(IsVersionLike)
        .ToList();

      return tags.Count == 0 ? null : tags.OrderBy(tag => tag, VersionComparer).Last();
    }
    catch (Exception)
    {
      return null;
    }
  }

  /// <summary>Whether a tag looks like a version (v1.2.3, 1.2, 2).</summary>
  public static bool IsVersionLike(string tag) =>
    tag.TrimStart('v', 'V').Split('.').All(part => part.Length > 0 && part.All(char.IsAsciiDigit));

  /// <summary>Compares version-like tags numerically (v1.10 &gt; v1.9).</summary>
  public static readonly IComparer<string> VersionComparer = Comparer<string>.Create((a, b) =>
  {
    var left = a.TrimStart('v', 'V').Split('.').Select(int.Parse).ToArray();
    var right = b.TrimStart('v', 'V').Split('.').Select(int.Parse).ToArray();
    for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
    {
      var l = i < left.Length ? left[i] : 0;
      var r = i < right.Length ? right[i] : 0;
      if (l != r)
        return l.CompareTo(r);
    }
    return 0;
  });
}
