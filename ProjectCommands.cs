namespace forge;

/// <summary>
/// Project-local commands: shell scripts in <c>.config/forge/commands/</c>
/// that <c>forge run &lt;name&gt;</c> can execute.
/// </summary>
/// <remarks>
/// They behave exactly like a <c>scripts</c> entry in <c>forge.lua</c>, except
/// they live in the repository as files (so they can be edited, reviewed and
/// executed directly). A name declared in <c>forge.lua</c> always wins.
/// </remarks>
public static class ProjectCommands
{
  public const string Directory = ".config/forge/commands";

  /// <summary>The shell command for a project-local command, or null.</summary>
  public static string? CommandFor(string name)
  {
    var path = Path.Combine(Directory, name + ".sh");
    return File.Exists(path) ? $"bash {path.Replace('\\', '/')}" : null;
  }

  /// <summary>The available command names, sorted.</summary>
  public static IEnumerable<string> Names() =>
    System.IO.Directory.Exists(Directory)
      ? System.IO.Directory.GetFiles(Directory, "*.sh")
          .Select(Path.GetFileNameWithoutExtension)
          .OrderBy(n => n)!
      : [];
}
