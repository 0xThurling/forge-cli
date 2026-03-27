using System.Diagnostics;
using System.IO.Compression;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands;

[CliCommand(
    Name = "download",
    Description = "Download files from the internet",
    Parent = typeof(RootCommand)
)]
public class DownloadCommand
{
  [CliArgument(Description = "URL to download")]
  public string URL { get; set; } = null!;

  [CliOption(Description = "Output file path")]
  public string Output { get; set; } = null!;

  [CliOption(Description = "Timeout in seconds (default: 300)")]
  public int Timeout { get; set; } = 300;

  public async Task<int> RunAsync()
  {
    try
    {
      AnsiConsole.MarkupLine($"[cyan]Downloading:[/] {URL}");
      using var client = new HttpClient();
      client.Timeout = TimeSpan.FromSeconds(Timeout);
      client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

      var bytes = await client.GetByteArrayAsync(URL);
      await File.WriteAllBytesAsync(Output, bytes);

      AnsiConsole.MarkupLine($"[green]Downloaded:[/] {Output} ({bytes.Length} bytes)");
      return 0;
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Download failed:[/] {ex.Message}");
      return 1;
    }
  }
}

[CliCommand(
    Name = "extract",
    Description = "Extract archive files.",
    Parent = typeof(RootCommand)
)]
public class ExtractCommand
{
  [CliArgument(Description = "Archive file to extract")]
  public string Archive { get; set; } = null!;

  [CliArgument(Description = "Output directory")]
  public string Output { get; set; } = null!;

  [CliOption(Description = "Strip components from path (default: 1)")]
  public int StripComponents { get; set; } = 1;

  public async Task<int> RunAsync()
  {
    try
    {
      AnsiConsole.MarkupLine($"[cyan]Extracting:[/] {Archive}");

      // Create output directory
      Directory.CreateDirectory(Output);

      // Handle different archive types
      var extension = Path.GetExtension(Archive).ToLower();

      if (extension is ".zip")
      {
        ZipFile.ExtractToDirectory(Archive, Output, true);
      }
      else if (extension is ".tar" or ".tgz" or ".tar.gz")
      {
        var process = new ProcessStartInfo("tar", $"-xzf \"{Archive}\" -C \"{Output}\" --strip-components={StripComponents}")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using var p = Process.Start(process);
        p?.WaitForExit();

        if (p?.ExitCode != 0)
        {
          AnsiConsole.MarkupLine($"[yellow]Warning:[/] tar extraction had issues");
        }
      }
      else
      {
        AnsiConsole.MarkupLine($"[red]Unsupported archive format:[/] {extension}");
        return 1;
      }

      AnsiConsole.MarkupLine($"[green]Extracted:[/] {Output}");
      return 0;
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Extraction failed:[/] {ex.Message}");
      return 1;
    }
  }
}

[CliCommand(
  Name = "fetch",
  Description = "Fetch and extract a remote archive in one step.",
  Parent = typeof(RootCommand)
)]
public class FetchCommand
{
  [CliArgument(Description = "URL to fetch")]
  public string URL { get; set; } = null!;

  [CliArgument(Description = "Output directory")]
  public string OutputDir { get; set; } = null!;

  [CliOption(Description = "Strip components from path")]
  public int StripComponents { get; set; } = 1;

  public async Task<int> RunAsync()
  {
    var tempFile = Path.Combine(Path.GetTempPath(), $"forge_fetch_{Guid.NewGuid()}.zip");

    try
    {
      // Download
      AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {URL}");

      using var client = new HttpClient();
      client.Timeout = TimeSpan.FromSeconds(300);
      client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

      var bytes = await client.GetByteArrayAsync(URL);
      await File.WriteAllBytesAsync(tempFile, bytes);

      // Extract
      AnsiConsole.MarkupLine($"[cyan]Extracting...[/]");
      Directory.CreateDirectory(OutputDir);

      ZipFile.ExtractToDirectory(tempFile, OutputDir, true);

      // Clean up temp
      File.Delete(tempFile);
      AnsiConsole.MarkupLine($"[green]Fetch complete:[/] {OutputDir}");
      return 0;
    }
    catch (Exception ex)
    {
      if (File.Exists(tempFile)) File.Delete(tempFile);
      AnsiConsole.MarkupLine($"[red]Fetch failed:[/] {ex.Message}");
      return 1;
    }
  }
}
