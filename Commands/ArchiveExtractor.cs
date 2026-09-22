using System.Diagnostics;
using System.IO.Compression;
using Spectre.Console;

namespace forge.Commands;

/// <summary>
/// Extracts zip and tar archives — the one implementation behind
/// <c>forge extract</c>, <c>forge fetch</c> and the Lua <c>forge.extract</c>.
/// </summary>
/// <remarks>
/// A failure is reported and returned, never swallowed: callers turn a non-zero
/// result into a non-zero exit code. <c>--strip-components</c> applies to tar
/// archives (zip has no equivalent in <see cref="ZipFile"/>).
/// </remarks>
internal static class ArchiveExtractor
{
  /// <returns>0 on success, 1 on failure.</returns>
  public static int Extract(string archive, string output, int stripComponents)
  {
    try
    {
      Directory.CreateDirectory(output);

      var extension = Path.GetExtension(archive).ToLowerInvariant();

      if (extension == ".zip")
      {
        // Zip has no strip option in ZipFile, so entries are walked by hand —
        // which also lets us refuse entries that escape the output directory.
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
          if (string.IsNullOrEmpty(entry.Name))
            continue; // directory entry

          var relative = StripLeadingComponents(entry.FullName, stripComponents);
          if (string.IsNullOrEmpty(relative))
            continue;

          var target = Path.GetFullPath(Path.Combine(output, relative));
          if (!target.StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar))
          {
            AnsiConsole.MarkupLine($"[red]Extraction failed:[/] '{entry.FullName}' escapes the output directory.");
            return 1;
          }

          Directory.CreateDirectory(Path.GetDirectoryName(target)!);
          entry.ExtractToFile(target, true);
        }
        return 0;
      }

      // ".gz" covers .tar.gz; `tar -xf` auto-detects the compression, unlike
      // `-xzf`, which rejects an uncompressed .tar.
      if (extension is ".tar" or ".tgz" or ".gz")
      {
        var psi = new ProcessStartInfo(
          "tar",
          $"-xf \"{archive}\" -C \"{output}\" --strip-components={stripComponents}")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
          AnsiConsole.MarkupLine("[red]Extraction failed:[/] could not start tar.");
          return 1;
        }

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
          AnsiConsole.MarkupLine(
            $"[red]Extraction failed:[/] tar exited with {process.ExitCode}: {stderr.Trim()}");
          return 1;
        }
        return 0;
      }

      AnsiConsole.MarkupLine($"[red]Unsupported archive format:[/] {extension}");
      return 1;
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Extraction failed:[/] {ex.Message}");
      return 1;
    }
  }

  /// <summary>
  /// Drops the first <paramref name="components"/> path segments, matching
  /// tar's <c>--strip-components</c>. Returns an empty string when nothing is
  /// left (a flat archive with components = 1 strips every entry).
  /// </summary>
  private static string StripLeadingComponents(string path, int components)
  {
    if (components <= 0)
      return path;

    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length <= components)
      return string.Empty;

    return string.Join('/', parts[components..]);
  }
}
