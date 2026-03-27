using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Displays a tree-like structure of the project's files and directories.
  /// </summary>
  /// <remarks>
  /// Shows the project's directory structure in a visual tree format, excluding
  /// common build artifacts like build/, .git/, obj/, and .cache/ directories.
  /// </remarks>
  /// <example>
  /// <code>
  /// forge project tree
  /// </code>
  /// </example>
  [CliCommand(Name = "tree", Description = "Display a tree-like structure of the project's files and directories.", Parent = typeof(ProjectCommand))]
  public class TreeCommand
  {
    /// <summary>
    /// Displays the project directory tree.
    /// </summary>
    /// <returns>0 on success, 1 if project root cannot be found.</returns>
    public async Task<int> Run()
    {
      var projectRoot = ProjectConfigManager.FindProjectRoot();
      if (projectRoot == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found.");
        return 1;
      }

      var root = new Tree(":open_file_folder: [yellow]" + new DirectoryInfo(projectRoot).Name + "[/]");

      AddDirectoryNodes(root, new DirectoryInfo(projectRoot), ["build", ".git", ".github", "obj", ".cache"]);

      AnsiConsole.Write(root);

      return 0;
    }

    private static void AddDirectoryNodes(IHasTreeNodes parent, DirectoryInfo directory, HashSet<string> excludedFolders)
    {
      foreach (var subDirectory in directory.GetDirectories().Where(d => !excludedFolders.Contains(d.Name)))
      {
        var subTree = parent.AddNode(":open_file_folder: [yellow]" + subDirectory.Name + "[/]");
        AddDirectoryNodes(subTree, subDirectory, excludedFolders);
      }

      foreach (var file in directory.GetFiles())
      {
        parent.AddNode(":page_facing_up: [blue]" + file.Name + "[/]");
      }
    }
  }
}
