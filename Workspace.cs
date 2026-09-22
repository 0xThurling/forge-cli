using System.Diagnostics;
using forge.Commands.Lua;
using forge.Models;
using Lua;
using Lua.Standard;
using Spectre.Console;

namespace forge;

/// <summary>A sibling project that belongs to a workspace.</summary>
internal sealed class WorkspaceProject
{
  public required string Name { get; init; }

  /// <summary>Absolute path to the project directory.</summary>
  public required string Directory { get; init; }

  public string Type { get; init; } = "executable";

  public string Version { get; init; } = string.Empty;

  /// <summary>Names of other workspace projects this one depends on.</summary>
  public List<string> DependsOn { get; } = [];
}

/// <summary>
/// Finds the sibling projects that make up a workspace and orders them by their
/// dependencies.
/// </summary>
/// <remarks>
/// A workspace is a directory containing several Forge projects. It is declared
/// with a <c>forge.workspace.lua</c> that lists them explicitly, or discovered
/// from the subdirectories when no such file exists. Dependencies between
/// projects are read from each project's <c>forge.lua</c>: a dependency is
/// considered local when its <c>path</c> points at another workspace project
/// (or when its name matches one).
/// </remarks>
internal static class Workspace
{
  private static readonly string[] IgnoredDirectories =
    ["build", ".config", ".git", ".cache", "external", "node_modules", "bin", "obj"];

  /// <summary>
  /// The workspace root: the nearest ancestor declaring
  /// <c>forge.workspace.lua</c>, otherwise the parent of the current project.
  /// </summary>
  public static string? FindRoot()
  {
    var directory = Directory.GetCurrentDirectory();
    while (directory != null)
    {
      if (File.Exists(Path.Combine(directory, "forge.workspace.lua")))
        return directory;
      directory = Directory.GetParent(directory)?.FullName;
    }

    var projectRoot = ProjectConfigManager.FindProjectRoot();
    if (projectRoot != null)
      return Directory.GetParent(projectRoot)?.FullName ?? projectRoot;

    // Not inside a project: the current directory may still be the workspace
    // root (that is where `forge workspace list` is most useful).
    return Directory.GetCurrentDirectory();
  }

  /// <summary>
  /// Loads every project in the workspace, ordered so that dependencies come
  /// first.
  /// </summary>
  public static async Task<List<WorkspaceProject>> DiscoverAsync(string root)
  {
    var directories = await CandidateDirectoriesAsync(root);
    var projects = new List<WorkspaceProject>();

    foreach (var directory in directories)
    {
      var config = await new LuaConfigLoader().LoadConfig(Path.Combine(directory, "forge.lua"));
      if (config == null || string.IsNullOrWhiteSpace(config.Project.Name))
        continue;

      projects.Add(new WorkspaceProject
      {
        Name = config.Project.Name,
        Directory = directory,
        Type = config.Project.Type,
        Version = config.Project.Version
      });
    }

    // Second pass: record the local dependency edges now that every project is known.
    for (var i = 0; i < projects.Count; i++)
    {
      var config = await new LuaConfigLoader().LoadConfig(Path.Combine(projects[i].Directory, "forge.lua"));
      if (config == null)
        continue;

      foreach (var (dependencyName, dependency) in config.Dependencies)
      {
        var match = Match(projects[i], dependencyName, dependency, projects);
        if (match != null && !projects[i].DependsOn.Contains(match.Name))
          projects[i].DependsOn.Add(match.Name);
      }
    }

    return Sort(projects);
  }

  /// <summary>Orders projects so that a project's dependencies build first.</summary>
  public static List<WorkspaceProject> Sort(List<WorkspaceProject> projects)
  {
    var byName = projects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
    var ordered = new List<WorkspaceProject>();
    var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void Visit(WorkspaceProject project)
    {
      if (done.Contains(project.Name) || !visiting.Add(project.Name))
        return; // done, or a dependency cycle: keep the discovery order

      foreach (var dependency in project.DependsOn)
      {
        if (byName.TryGetValue(dependency, out var target))
          Visit(target);
      }

      visiting.Remove(project.Name);
      done.Add(project.Name);
      ordered.Add(project);
    }

    foreach (var project in projects)
      Visit(project);

    return ordered;
  }

  /// <summary>The workspace project a dependency refers to, or null.</summary>
  private static WorkspaceProject? Match(
    WorkspaceProject owner,
    string dependencyName,
    Dependency dependency,
    List<WorkspaceProject> projects)
  {
    if (!string.IsNullOrWhiteSpace(dependency.Path))
    {
      var resolved = Normalize(Path.Combine(owner.Directory, dependency.Path));
      var byPath = projects.FirstOrDefault(project => Normalize(project.Directory) == resolved);
      if (byPath != null)
        return byPath;
    }

    return projects.FirstOrDefault(project =>
      !ReferenceEquals(project, owner) &&
      (string.Equals(project.Name, dependencyName, StringComparison.OrdinalIgnoreCase) ||
       string.Equals(Path.GetFileName(project.Directory), dependencyName, StringComparison.OrdinalIgnoreCase)));
  }

  private static string Normalize(string path) =>
    Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

  /// <summary>
  /// The project directories to load: those listed in
  /// <c>forge.workspace.lua</c>, or every subdirectory holding a
  /// <c>forge.lua</c>.
  /// </summary>
  private static async Task<List<string>> CandidateDirectoriesAsync(string root)
  {
    var workspaceFile = Path.Combine(root, "forge.workspace.lua");
    if (File.Exists(workspaceFile))
    {
      var listed = await ReadWorkspaceFileAsync(workspaceFile);
      return listed
        .Select(entry => Path.GetFullPath(Path.Combine(root, entry)))
        .Where(System.IO.Directory.Exists)
        .ToList();
    }

    var directories = new List<string>();
    foreach (var directory in System.IO.Directory.EnumerateDirectories(root))
    {
      if (IsIgnored(directory))
        continue;

      if (File.Exists(Path.Combine(directory, "forge.lua")))
      {
        directories.Add(directory);
        continue;
      }

      // One level deeper, so grouped layouts (libs/foo) still work.
      foreach (var nested in System.IO.Directory.EnumerateDirectories(directory))
      {
        if (!IsIgnored(nested) && File.Exists(Path.Combine(nested, "forge.lua")))
          directories.Add(nested);
      }
    }

    directories.Sort(StringComparer.Ordinal);
    return directories;
  }

  private static bool IsIgnored(string directory)
  {
    var name = Path.GetFileName(directory);
    return name.StartsWith('.') || IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>Reads the <c>projects</c> list from a workspace file.</summary>
  private static async Task<List<string>> ReadWorkspaceFileAsync(string path)
  {
    var projects = new List<string>();
    try
    {
      var state = LuaState.Create();
      state.OpenStandardLibraries();
      var result = await state.DoFileAsync(path);
      if (result.Length == 0 || !result[0].TryRead<LuaTable>(out var table))
        return projects;

      if (table["projects"].TryRead<LuaTable>(out var listed))
      {
        foreach (var entry in listed)
        {
          // Only the array part: `name = "..."` style keys are not projects.
          if (!entry.Key.TryRead<double>(out _))
            continue;

          var value = entry.Value.ToString();
          if (!string.IsNullOrWhiteSpace(value))
            projects.Add(value);
        }
      }
    }
    catch (Exception exception)
    {
      AnsiConsole.MarkupLine($"[yellow]Could not read {Path.GetFileName(path)}: {exception.Message}[/]");
    }

    return projects;
  }
}

/// <summary>
/// Re-runs the Forge CLI in another directory, so workspace sub-builds use
/// exactly the same code path as a normal <c>forge build</c>.
/// </summary>
internal static class SelfInvocation
{
  /// <summary>The command line that re-runs this CLI.</summary>
  public static (string FileName, List<string> Arguments) Command()
  {
    var entry = Environment.GetCommandLineArgs().FirstOrDefault() ?? "forge";
    if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
      return (Environment.ProcessPath ?? "dotnet", [Path.GetFullPath(entry)]);

    return (Path.GetFullPath(entry), []);
  }

  public static async Task<int> RunAsync(string workingDirectory, IReadOnlyList<string> arguments)
  {
    var (fileName, prefix) = Command();
    var psi = new ProcessStartInfo(fileName)
    {
      WorkingDirectory = workingDirectory,
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in prefix)
      psi.ArgumentList.Add(argument);
    foreach (var argument in arguments)
      psi.ArgumentList.Add(argument);

    using var process = Process.Start(psi);
    if (process == null)
      return 1;

    await process.WaitForExitAsync();
    return process.ExitCode;
  }
}
