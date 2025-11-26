using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
    [CliCommand(Name = "tree", Description = "Display a tree-like structure of the project's files and directories.", Parent = typeof(ProjectCommand))]
    public class TreeCommand
    {
        public int Run()
        {
            var projectRoot = ProjectConfigManager.FindProjectRoot();
            if (projectRoot == null)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Not a forge project. `package.toml` not found.");
                return 1;
            }

            var root = new Tree(":open_file_folder: [yellow]" + new DirectoryInfo(projectRoot).Name + "[/]");

            AddDirectoryNodes(root, new DirectoryInfo(projectRoot), new HashSet<string> { "build", ".git", ".github", "obj", ".cache" });

            AnsiConsole.Write(root);

            return 0;
        }

        private void AddDirectoryNodes(IHasTreeNodes parent, DirectoryInfo directory, HashSet<string> excludedFolders)
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
