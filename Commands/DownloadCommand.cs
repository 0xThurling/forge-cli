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

  [CliOption(Description = "Output file path (default: the URL's file name)", Required = false)]
  public string? Output { get; set; }

  [CliOption(Description = "Timeout in seconds (default: 300)")]
  public int Timeout { get; set; } = 300;

  [CliOption(Description = "Expected SHA256 hash for verification", Required = false)]
  public string? Sha256 { get; set; }

  [CliOption(Description = "Show progress bar")]
  public bool ShowProgress { get; set; }

  public async Task<int> RunAsync()
  {
    // No --output: use the URL's file name, the way `curl -O` does. Resolved
    // before the try so the failure path can remove a partial file.
    var output = Output;
    if (string.IsNullOrWhiteSpace(output))
    {
      try
      {
        output = Path.GetFileName(new Uri(URL).AbsolutePath);
      }
      catch (UriFormatException)
      {
        output = string.Empty;
      }

      if (string.IsNullOrWhiteSpace(output))
      {
        AnsiConsole.MarkupLine(
          "[bold red]Error:[/] the URL has no file name — pass `--output <file>`.");
        return 1;
      }
    }

    try
    {
      using var client = new HttpClient();
      client.Timeout = TimeSpan.FromSeconds(Timeout);
      client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

      AnsiConsole.MarkupLine($"[cyan]Downloading:[/] {URL}");

      using var response = await client.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead);
      response.EnsureSuccessStatusCode();

      var totalBytes = response.Content.Headers.ContentLength ?? -1;

      long totalRead = 0;

      // Scope the writer so the file is closed before the hash is computed:
      // the stream is write-only (and exclusive), so hashing it in place fails.
      await using (var contentStream = await response.Content.ReadAsStreamAsync())
      await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
      {
        var buffer = new byte[8192];
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

        // A live progress display needs a terminal. When the output is
        // captured (CI, a pipe, a test) it is noise at best — and the live
        // display is the only part of a download that behaves differently
        // there, so it is skipped instead of risking the download.
        if (ShowProgress && totalBytes > 0 && AnsiConsole.Profile.Capabilities.Interactive)
        {
          AnsiConsole.Progress()
              .Start(ctx =>
              {
                // The description is markup: an unclosed tag throws while the
                // progress display refreshes (and truncates the download).
                var task = ctx.AddTask("[cyan]Downloading[/]", maxValue: totalBytes);
                while (!downloadTask.IsCompleted)
                {
                  task.Value = totalRead;
                  Thread.Sleep(100);
                }
                task.Value = totalRead;
              });
        }

        // Always await the task, progress or not: otherwise a failure inside it
        // (a locked output file, a dropped connection) is never observed and
        // the command reports success for a truncated file.
        await downloadTask;
      }

      // Verify SHA256 if provided
      if (!string.IsNullOrEmpty(Sha256))
      {
        using var readStream = File.OpenRead(output);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(readStream);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!hash.Equals(Sha256, StringComparison.InvariantCultureIgnoreCase))
        {
          AnsiConsole.MarkupLine($"[red]SHA256 verification failed! Expected: {Sha256}, Got: {hash}[/]");
          File.Delete(output);
          return 1;
        }
        AnsiConsole.MarkupLine($"[green]SHA256 verification passed[/]");
      }
      AnsiConsole.MarkupLine($"[green]Downloaded:[/] {output} ({totalRead} bytes)");
      return 0;
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Download failed:[/] {ex.Message}");

      // Never leave a truncated file behind: it looks like a completed
      // download to every later step (and to `--sha-256`, which would then
      // report a mismatch instead of the real failure).
      try
      {
        if (File.Exists(output))
          File.Delete(output);
      }
      catch (IOException)
      {
        // Nothing more to do; the failure above is the message that matters.
      }

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

  public Task<int> RunAsync()
  {
    AnsiConsole.MarkupLine($"[cyan]Extracting:[/] {Archive}");

    var result = ArchiveExtractor.Extract(Archive, Output, StripComponents);
    if (result == 0)
      AnsiConsole.MarkupLine($"[green]Extracted:[/] {Output}");

    return Task.FromResult(result);
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

  [CliOption(Description = "Expected SHA256 hash for verification", Required = false)]
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

      // The writer is scoped so the file is closed before hashing (the stream
      // is write-only and exclusive).
      await using (var contentStream = await response.Content.ReadAsStreamAsync())
      await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
      {
        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
          await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }
      }

      // Verify SHA256 if provided
      if (!string.IsNullOrEmpty(Sha256))
      {
        using var readStream = File.OpenRead(tempFile);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(readStream);
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

      // Archive type comes from the URL: the temp file is always named .zip.
      var archiveName = Path.GetFileName(new Uri(URL).AbsolutePath);
      var staging = Path.Combine(
        Path.GetDirectoryName(tempFile)!,
        $"forge_fetch_{Guid.NewGuid()}{Path.GetExtension(archiveName)}");
      File.Move(tempFile, staging, true);

      var result = ArchiveExtractor.Extract(staging, OutputDir, StripComponents);
      File.Delete(staging);
      if (result != 0)
        return result;

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
