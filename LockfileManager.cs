using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using forge.Models;
using Spectre.Console;

namespace forge;

/// <summary>
/// Reads, writes and resolves <c>forge.lock</c>.
/// </summary>
public static class LockfileManager
{
  public const string FileName = "forge.lock";

  /// <summary>Loads the lock, or an empty one when the file does not exist.</summary>
  public static Lockfile Load()
  {
    var path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
    if (!File.Exists(path))
      return new Lockfile();

    try
    {
      return JsonSerializer.Deserialize(File.ReadAllText(path), LockJsonContext.Default.Lockfile)
             ?? new Lockfile();
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[yellow]Warning:[/] ignoring an unreadable {FileName}: {ex.Message}");
      return new Lockfile();
    }
  }

  public static void Save(Lockfile lockfile)
  {
    var path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
    File.WriteAllText(path, JsonSerializer.Serialize(lockfile, LockJsonContext.Default.Lockfile));
  }

  /// <summary>
  /// The locked commit for a declared dependency, or null when there is none
  /// (or the declaration changed since it was locked).
  /// </summary>
  public static string? LockedCommitFor(string name, Dependency dependency)
  {
    var lockfile = Load();
    if (!lockfile.Git.TryGetValue(name, out var locked))
      return null;

    // A changed repository or ref invalidates the entry: the user is asking for
    // something else now.
    if (!string.Equals(locked.Git, dependency.Git, StringComparison.Ordinal) ||
        !string.Equals(locked.Tag, dependency.Tag, StringComparison.Ordinal))
      return null;

    return string.IsNullOrWhiteSpace(locked.Commit) ? null : locked.Commit;
  }

  /// <summary>
  /// Resolves every git dependency that is missing from the lock (or all of
  /// them with <paramref name="force"/>) and drops entries that no longer
  /// correspond to a declared dependency.
  /// </summary>
  /// <returns>The number of entries added or refreshed.</returns>
  public static async Task<int> ResolveAsync(ProjectConfig config, bool force)
  {
    var lockfile = Load();
    var resolved = 0;

    foreach (var (name, dependency) in config.Dependencies)
    {
      if (!string.IsNullOrWhiteSpace(dependency.Path))
        continue; // local checkouts are never locked

      if (string.IsNullOrWhiteSpace(dependency.Git) || string.IsNullOrWhiteSpace(dependency.Tag))
        continue;

      if (!force &&
          lockfile.Git.TryGetValue(name, out var existing) &&
          string.Equals(existing.Git, dependency.Git, StringComparison.Ordinal) &&
          string.Equals(existing.Tag, dependency.Tag, StringComparison.Ordinal) &&
          !string.IsNullOrWhiteSpace(existing.Commit))
      {
        continue; // already locked and unchanged
      }

      var commit = await ResolveRefAsync(dependency.Git, dependency.Tag);
      if (commit is null)
      {
        AnsiConsole.MarkupLine(
          $"[yellow]Warning:[/] could not resolve '{dependency.Tag}' of '{name}' ({dependency.Git}); leaving it unlocked.");
        continue;
      }

      lockfile.Git[name] = new LockedDependency
      {
        Git = dependency.Git,
        Tag = dependency.Tag,
        Commit = commit
      };
      resolved++;
    }

    var stale = lockfile.Git.Keys
      .Where(name => !config.Dependencies.ContainsKey(name))
      .ToList();
    foreach (var name in stale)
      lockfile.Git.Remove(name);

    if (resolved > 0 || stale.Count > 0)
    {
      Save(lockfile);
      AnsiConsole.MarkupLine(
        $"[green]Locked[/] {resolved} dependenc{(resolved == 1 ? "y" : "ies")}" +
        (stale.Count > 0 ? $", dropped {stale.Count} stale entr{(stale.Count == 1 ? "y" : "ies")}" : "") +
        $" → {FileName}");
    }

    return resolved;
  }

  /// <summary>
  /// Resolves a ref to a commit with <c>git ls-remote</c>. An annotated tag
  /// reports both the tag object and a peeled <c>^{}</c> line — the peeled one
  /// is the commit a checkout should use.
  /// </summary>
  private static async Task<string?> ResolveRefAsync(string repo, string reference)
  {
    // A commit hash is already the resolved answer. `git ls-remote` only lists
    // refs, so asking it about a hash would fail — and leave a dependency that
    // is pinned by commit (the hot-reload engine, or a deliberate choice)
    // unlocked.
    if (reference.Length is >= 7 and <= 40 && reference.All(Uri.IsHexDigit))
      return reference.ToLowerInvariant();

    try
    {
      var psi = new ProcessStartInfo("git", $"ls-remote \"{repo}\" \"{reference}\"")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      using var process = Process.Start(psi);
      if (process == null)
        return null;

      var output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();
      if (process.ExitCode != 0)
        return null;

      string? peeled = null;
      string? direct = null;
      foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
      {
        var parts = line.Split('\t');
        if (parts.Length != 2)
          continue;

        if (parts[1].EndsWith("^{}", StringComparison.Ordinal))
          peeled = parts[0].Trim();
        else if (direct is null)
          direct = parts[0].Trim();
      }

      return peeled ?? direct;
    }
    catch (Exception)
    {
      return null;
    }
  }
}

/// <summary>
/// Source-generated serialization for <c>forge.lock</c>: the CLI is published
/// with AOT, where reflection-based JSON is disabled.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             WriteIndented = true)]
[JsonSerializable(typeof(Lockfile))]
internal partial class LockJsonContext : JsonSerializerContext
{
}
