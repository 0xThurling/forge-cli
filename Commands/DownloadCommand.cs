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
  
  [CliOption(Description = "Expected SHA256 hash for verification")]
  public string? Sha256 { get; set; }
 
  [CliOption(Description = "Show progress bar")]
  public bool ShowProgress { get; set; }

  public async Task<int> RunAsync()
  {
    try
    {
      using var client = new HttpClient();
      client.Timeout = TimeSpan.FromSeconds(Timeout);
      client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");
 
      AnsiConsole.MarkupLine($"[cyan]Downloading:[/] {URL}");
      
      using var response = await client.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead);
      response.EnsureSuccessStatusCode();
      
      var totalBytes = response.Content.Headers.ContentLength ?? -1;
      
      await using var contentStream = await response.Content.ReadAsStreamAsync();
      await using var fileStream = new FileStream(Output, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
      
      var buffer = new byte[8192];
      long totalRead = 0;
      int bytesRead;
      
      // Progress tracking
      var downloadTask = Task.Run(async () =>
      {
        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
          await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
          totalRead += bytesRead;
        }
      });
      
      if (ShowProgress && totalBytes > 0)
      {
        AnsiConsole.Progress()
            .Start(ctx =>
            {
              var task = ctx.AddTask("[cyan]Downloading", maxValue: totalBytes);
              while (!downloadTask.IsCompleted)
              {
                task.Value = totalRead;
                Thread.Sleep(100);
              }
              task.Value = totalRead;
            });
      }
      else
      {
        await downloadTask;
      }
      // Verify SHA256 if provided
      if (!string.IsNullOrEmpty(Sha256))
      {
        fileStream.Position = 0;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!hash.Equals(Sha256, StringComparison.InvariantCultureIgnoreCase))
        {
          AnsiConsole.MarkupLine($"[red]SHA256 verification failed! Expected: {Sha256}, Got: {hash}[/]");
          File.Delete(Output);
          return 1;
        }
        AnsiConsole.MarkupLine($"[green]SHA256 verification passed[/]");
      }
      AnsiConsole.MarkupLine($"[green]Downloaded:[/] {Output} ({totalRead} bytes)");
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
      Directory.CreateDirectory(Output);
      
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
  
  [CliOption(Description = "Expected SHA256 hash for verification")]
  public string? Sha256 { get; set; }
  
  public async Task<int> RunAsync()
  {
    var tempFile = Path.Combine(Path.GetTempPath(), $"forge_fetch_{Guid.NewGuid()}.zip");
    try
    {
      AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {URL}");
      using var client = new HttpClient();
      client.Timeout = TimeSpan.FromSeconds(300);
      client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");
 
      using var response = await client.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead);
      response.EnsureSuccessStatusCode();
      
      var totalBytes = response.Content.Headers.ContentLength ?? -1;
      
      await using var contentStream = await response.Content.ReadAsStreamAsync();
      await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
      
      var buffer = new byte[8192];
      long totalRead = 0;
      int bytesRead;

      while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
      {
        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        totalRead += bytesRead;
      }

      // Verify SHA256 if provided
      if (!string.IsNullOrEmpty(Sha256))
      {
        fileStream.Position = 0;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!hash.Equals(Sha256, StringComparison.InvariantCultureIgnoreCase))
        {
          AnsiConsole.MarkupLine($"[red]SHA256 verification failed![/]");
          File.Delete(tempFile);
          return 1;
        }
        AnsiConsole.MarkupLine($"[green]SHA256 verification passed[/]");
      }
      AnsiConsole.MarkupLine($"[cyan]Extracting...[/]");

      Directory.CreateDirectory(OutputDir);

      ZipFile.ExtractToDirectory(tempFile, OutputDir, true);

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
