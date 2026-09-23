namespace forge;

/// <summary>
/// The user's shell profile, for the PATH and environment entries a setup needs.
/// </summary>
/// <remarks>
/// Only ever appends, and only once: every entry is written with a marker
/// comment, and an entry already present is left alone. Forge does not replace
/// or reorder anything you wrote.
/// </remarks>
internal static class ShellProfile
{
  private const string Marker = "# added by forge setup";

  /// <summary>The profile for the user's shell, or ~/.profile as a fallback.</summary>
  public static string Path()
  {
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var shell = Environment.GetEnvironmentVariable("SHELL") ?? string.Empty;

    if (shell.EndsWith("zsh", StringComparison.Ordinal))
      return System.IO.Path.Combine(home, ".zshrc");
    if (shell.EndsWith("bash", StringComparison.Ordinal))
      return System.IO.Path.Combine(home, ".bashrc");
    if (shell.EndsWith("fish", StringComparison.Ordinal))
      return System.IO.Path.Combine(home, ".config", "fish", "config.fish");

    return System.IO.Path.Combine(home, ".profile");
  }

  /// <summary>
  /// Adds a line to the profile unless it is already there. Returns false when
  /// it was present (or could not be written), so the caller can say which.
  /// </summary>
  public static bool Ensure(string line)
  {
    var path = Path();
    try
    {
      var existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
      if (existing.Contains(line, StringComparison.Ordinal))
        return false;

      var addition = (existing.EndsWith('\n') || existing.Length == 0 ? string.Empty : "\n") +
                     $"{Marker}\n{line}\n";
      Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
      File.AppendAllText(path, addition);
      return true;
    }
    catch (Exception)
    {
      return false;
    }
  }
}
