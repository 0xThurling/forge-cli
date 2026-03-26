using DotMake.CommandLine;

namespace forge.Commands;

[CliCommand(
    Name = "download",
    Description = "Download files from the internet",
    Parent = typeof(RootCommand)
)]
public class DownloadCommand
{

}
