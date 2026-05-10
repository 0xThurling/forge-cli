using System.Diagnostics;
using System.IO.Compression;
using forge.ForgeEngine.CoreUtils;
using Lua;
using Spectre.Console;

namespace forge.Commands.Lua;

public abstract class LuaFunctionModule
{
  public abstract string ModuleName { get; }
  public abstract void RegisterFunctions(ref LuaTable table);
}

public class CoreFunctionModule : LuaFunctionModule
{
  public override string ModuleName => "forge";
  public override void RegisterFunctions(ref LuaTable table)
  {
    // Nested table
    var log = new LuaTable();
    log[new LuaValue("info")] = new LuaValue(CreateInfoLogFunction());
    log[new LuaValue("warn")] = new LuaValue(CreateWarnLogFunction());
    log[new LuaValue("error")] = new LuaValue(CreateErrorLogFunction());

    var configTable = new LuaTable();
    configTable[new LuaValue("get")] = new LuaValue(CreateConfigGetFunction());
    configTable[new LuaValue("has_feature")] = new LuaValue(CreateConfigHasFeatureFunction());
    configTable[new LuaValue("get_feature_option")] = new LuaValue(CreateGetFeatureFunction());

    // Core Functions
    table[new LuaValue("pull_repo")] = new LuaValue(CreatePullRepoFunction());
    table[new LuaValue("get_packages")] = new LuaValue(CreateGetPackagesFunction());
    table[new LuaValue("add_cmake")] = new LuaValue(CreateCustomCMakeFunction());
    table[new LuaValue("download")] = new LuaValue(CreateDownloadFunction());
    table[new LuaValue("extract")] = new LuaValue(CreateExtractFunction());
    table[new LuaValue("fetch")] = new LuaValue(CreateFetchFunction());

    // Register nester tables
    table[new LuaValue("log")] = new LuaValue(log);
    table[new LuaValue("config")] = new LuaValue(configTable);
  }

  // Provides forge.pull_repo(url) function that clones a Git repository
  private static LuaFunction CreatePullRepoFunction() =>
    new("pull_repo", async (context, token) =>
    {
      var repoUrl = context.GetArgument<string>(0);
      var tag = context.GetArgument<string?>(1);

      var repoName = repoUrl.Split('/')[^1].Split(".")[0];

      var gitCommand = string.IsNullOrEmpty(tag) ?
      $"clone {repoUrl} external/{repoName}" :
      $"clone --depth 1 --branch {tag} {repoUrl} external/{repoName}";

      var processStartInfo = new ProcessStartInfo("git", gitCommand)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };

      using var process = Process.Start(processStartInfo) ?? throw new Exception("Failed to pull repository");

      AnsiConsole.WriteLine("Cloning repo...");

      await process.WaitForExitAsync(token);

      return process.ExitCode;
    });

  // Log info
  private static LuaFunction CreateInfoLogFunction() =>
      new("info", (context, _) =>
      {
        try
        {
          var info = context.GetArgument<string>(0);
          AnsiConsole.MarkupLineInterpolated($"[bold blue]INFO[/]: {info}");
        }
        catch (Exception ex)
        {
          AnsiConsole.WriteLine($"DEBUG ERROR: {ex.Message}");
        }
        return ValueTask.FromResult(0);
      });

  private static LuaFunction CreateWarnLogFunction() =>
      new("warn", (context, _) =>
      {
        try
        {
          var info = context.GetArgument<string>(0);
          AnsiConsole.MarkupLineInterpolated($"[bold blue]WARN[/]: {info}");
        }
        catch (Exception ex)
        {
          AnsiConsole.WriteLine($"DEBUG ERROR: {ex.Message}");
        }
        return ValueTask.FromResult(0);
      });

  private static LuaFunction CreateErrorLogFunction() =>
      new("error", (context, _) =>
      {
        try
        {
          var info = context.GetArgument<string>(0);
          AnsiConsole.MarkupLineInterpolated($"[bold red]ERROR[/]: {info}");
        }
        catch (Exception ex)
        {
          AnsiConsole.WriteLine($"DEBUG ERROR: {ex.Message}");
        }
        return ValueTask.FromResult(0);
      });

  private static LuaFunction CreateGetPackagesFunction() =>
      new("get_packages", async (context, token) =>
      {
        var password = context.GetArgument<string>(0);
        var packageManager = context.GetArgument<string>(1);
        var packages = context.GetArgument<LuaTable>(2);

        var packageList = packages.Select(x => x.Value.ToString()).ToList();

        if (password == "nopass")
        {
          CoreUtils.InstallPackages(false, packageManager, packageList);
        }
        else
        {
          CoreUtils.InstallPackages(true, packageManager, packageList, password);
        }

        return 0;
      });

  private static LuaFunction CreateCustomCMakeFunction() =>
    new("add_cmake", (context, token) =>
    {
      var cmakeSnippet = context.GetArgument<string>(0);

      ProjectBuildManager.CustomCmakeSnippets.Add(cmakeSnippet);

      AnsiConsole.MarkupLine($"[green]Added custom CMake snippet[/]");

      return ValueTask.FromResult(0);
    });

  // forge.config.get(key) - get config key
  private static LuaFunction CreateConfigGetFunction() =>
      new("config_get", async (context, token) =>
      {
        var key = context.GetArgument<string>(0);

        var config = await ProjectConfigManager.LoadConfigAsync();
        if (config == null)
        {
          context.Return(LuaValue.Nil);
          return 1;
        }

        var value = LuaEngine.GetConfigValue(config, key);
        context.Return(value ?? "");
        return 1;
      });

  // forge.config.set(key, value) - Set config value (for runtime modification)
  private static LuaFunction CreateConfigSetFunction() =>
      new("config_set", async (context, token) =>
      {
        var key = context.GetArgument<string>(0);
        var value = context.GetArgument<string>(1);

        await LuaEngine.SetConfigValue(key, value);
        AnsiConsole.MarkupLine($"[green]Config set: {key} = {value}[/]");

        return 0;
      });

  // forge.config.has_feature(name) - Check if feature is enabled
  private static LuaFunction CreateConfigHasFeatureFunction() =>
      new("config_has_feature", async (context, token) =>
      {
        var feature = context.GetArgument<string>(0);

        var config = await ProjectConfigManager.LoadConfigAsync();
        var hasFeature = config?.Features.ContainsKey(feature) == true &&
            config.Features[feature].Enabled;

        context.Return(hasFeature);
        return 1;
      });

  // forge.config.get_feature_option(feature, option, default)
  private static LuaFunction CreateGetFeatureFunction() =>
      new("config_get_feature_option", async (context, token) =>
      {
        var feature = context.GetArgument<string>(0);
        var option = context.GetArgument<string>(1);
        var defaultValue = context.GetArgument<string>(2);

        var config = await ProjectConfigManager.LoadConfigAsync();
        if (config?.Features.TryGetValue(feature, out var featureConfig) == true &&
            featureConfig.Options.TryGetValue(option, out var value))
        {
          context.Return(value);
          return 1;
        }

        context.Return(defaultValue);
        return 1;
      });

  // forge.download(url, output, options?, progress_callback?)
  // Options: { timeout = 300, sha256 = "..." }
  // Progress callback receives: (bytes_downloaded, total_bytes)
  private static LuaFunction CreateDownloadFunction() =>
      new("download", async (context, token) =>
      {
        var url = context.GetArgument<string>(0);
        var output = context.GetArgument<string>(1);

        // Optional options table: { timeout = 300, sha256 = "..." }
        Dictionary<string, string>? options = null;
        try
        {
          var luaTable = context.GetArgument<LuaTable>(2);
          options = [];
          foreach (var (key, value) in luaTable)
          {
            options[key.ToString()] = value.ToString();
          }
        }
        catch { }
        // Optional progress callback function
        LuaFunction? progressCallback = null;
        try { progressCallback = context.GetArgument<LuaFunction>(3); } catch { }
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

        // Use streaming to avoid memory issues with large files
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(token);
        await using var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        long lastReported = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, token)) > 0)
        {
          await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
          totalRead += bytesRead;

          // Report progress every 1% or every 64KB (whichever comes first)
          if (progressCallback != null && totalBytes > 0)
          {
            var percent = totalRead * 100 / totalBytes;
            if (percent > lastReported || totalRead - lastReported > 65536)
            {
              lastReported = percent;
              // Call Lua callback using the state's CallAsync
              var state = context.State;
              var basePos = state.Stack.Count;
              state.Push(progressCallback);
              state.Push(new LuaValue(totalRead));
              state.Push(new LuaValue(totalBytes));
              await state.CallAsync(basePos, basePos, token);
            }
          }
        }
        AnsiConsole.MarkupLine($"[green]Downloaded:[/] {output} ({totalRead} bytes)");

        // Return downloaded size for verification
        context.Return(totalRead);
        return 1;
      });

  // forge.extract(archive_path, output_dir, string_components?)
  private static LuaFunction CreateExtractFunction() =>
      new("extract", (context, token) =>
      {
        var archive = context.GetArgument<string>(0);
        var output = context.GetArgument<string>(1);
        var stripComponents = context.GetArgument<int>(2);

        try
        {
          Directory.CreateDirectory(output);

          var ext = Path.GetExtension(archive).ToLower();
          if (ext is ".zip")
          {
            ZipFile.ExtractToDirectory(archive, output, true);
          }
          else if (ext is ".tar" or ".tgz" or ".gz")
          {
            var psi = new ProcessStartInfo("tar", $"-xf \"{archive}\" -c \"{output}\" --strip-components={stripComponents}")
            {
              UseShellExecute = false,
              RedirectStandardOutput = true,
              RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            p?.WaitForExit();
          }

          AnsiConsole.MarkupLine($"[green]Extracted:[/] {output}");
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Extraction failed:[/] {ex.Message}");
          return ValueTask.FromResult(1);
        }

        return ValueTask.FromResult(0);
      });

  // forge.fetch(url, output_dir?) - download and extract to external/<name>
  // Returns the path to the extracted directory
  private static LuaFunction CreateFetchFunction() =>
      new("fetch", async (context, token) =>
      {
        var url = context.GetArgument<string>(0);
        string? outputOverride = null;

        // Check if second argument is provided (could be output dir or nil)
        try { outputOverride = context.GetArgument<string>(1); } catch { }

        // Derive simple name from URL (like pull_repo does)
        var urlParts = url.Split('/');
        var lastPart = urlParts[^1];
        var archiveName = lastPart.Split('.')[0]; // Remove extension

        // Remove common prefixes like "archive/" or "refs/tags/"
        if (archiveName.StartsWith("archive") || archiveName.StartsWith("refs"))
        {
          archiveName = urlParts.Length > 1 ? urlParts[^2] : archiveName;
        }

        // Default to external/<name> if no output specified
        var output = outputOverride ?? Path.Combine("external", archiveName);

        var tempFile = Path.Combine(Path.GetTempPath(), $"forge_fetch_{Guid.NewGuid()}.zip");
        try
        {
          // Download
          using var client = new HttpClient();
          client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");
          AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {url}");
          var bytes = await client.GetByteArrayAsync(url, token);
          await File.WriteAllBytesAsync(tempFile, bytes, token);
          // Extract to temp location first
          var tempExtractDir = Path.Combine(Path.GetTempPath(), $"forge_fetch_extract_{Guid.NewGuid()}");
          Directory.CreateDirectory(tempExtractDir);
          ZipFile.ExtractToDirectory(tempFile, tempExtractDir, true);
          // Find the single root directory and copy contents to final output
          var entries = Directory.GetDirectories(tempExtractDir);
          var rootDir = entries.Length == 1 ? entries[0] : null;

          Directory.CreateDirectory(output);

          if (rootDir != null && Directory.GetFiles(rootDir).Length == 0 && Directory.GetDirectories(rootDir).Length == 0)
          {
            // Root dir is empty folder, use its contents
            rootDir = Directory.GetDirectories(tempExtractDir)[0];
          }

          // Helper to copy a directory's contents (handles cross-device moves)
          void CopyDirectoryContents(string src, string dest)
          {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
            {
              var destFile = Path.Combine(dest, Path.GetFileName(file));
              File.Copy(file, destFile, true);
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
              var destDir = Path.Combine(dest, Path.GetFileName(dir));
              CopyDirectoryContents(dir, destDir);
            }
          }
          if (rootDir != null)
          {
            // Copy all contents from rootDir to output (copy handles cross-device)
            CopyDirectoryContents(rootDir, output);
          }
          else
          {
            // No nested structure, files directly in tempExtractDir
            foreach (var file in Directory.GetFiles(tempExtractDir))
            {
              var destFile = Path.Combine(output, Path.GetFileName(file));
              File.Copy(file, destFile, true);
            }
          }
          // Cleanup temp files
          if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
          if (File.Exists(tempFile)) File.Delete(tempFile);
          AnsiConsole.MarkupLine($"[green]Fetch complete:[/] {output}");

          // Return the output path for use in Lua
          context.Return(output);
          return 1;
        }
        catch (Exception ex)
        {
          if (File.Exists(tempFile)) File.Delete(tempFile);
          AnsiConsole.MarkupLine($"[red]Fetch failed:[/] {ex.Message}");
          return 1;
        }
      });
}
