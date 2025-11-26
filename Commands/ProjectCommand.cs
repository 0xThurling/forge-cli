using DotMake.CommandLine;

namespace forge.Commands
{
    [CliCommand(Name = "project", Description = "Commands for managing the project.", Parent = typeof(RootCommand))]
    public class ProjectCommand
    {
    }
}
