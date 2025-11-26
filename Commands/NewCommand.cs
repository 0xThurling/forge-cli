using DotMake.CommandLine;

namespace forge.Commands
{
    [CliCommand(Description = "Create a new entity.", Parent = typeof(RootCommand))]
    public class NewCommand
    {
    }
}
