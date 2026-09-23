using System.Diagnostics;
using System.IO.Compression;
using forge.CMakeGeneration;
using forge.ForgeEngine.CoreUtils;
using forge.Models;
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
  private readonly BuildContext _context;

  /// <summary>
  /// The module contributes to the build it was created for: the functions
  /// capture that context, so contributions cannot leak between builds.
  /// </summary>
  public CoreFunctionModule(BuildContext context) => _context = context;

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
    configTable[new LuaValue("set")] = new LuaValue(CreateConfigSetFunction());
    configTable[new LuaValue("has_feature")] = new LuaValue(CreateConfigHasFeatureFunction());
    configTable[new LuaValue("get_feature_option")] = new LuaValue(CreateGetFeatureFunction());

    // Core Functions
    table[new LuaValue("pull_repo")] = new LuaValue(CreatePullRepoFunction());
    table[new LuaValue("get_packages")] = new LuaValue(CreateGetPackagesFunction());
    table[new LuaValue("add_cmake")] = new LuaValue(CreateCustomCMakeFunction());
    table[new LuaValue("add_section")] = new LuaValue(CreateAddSectionFunction());
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
      // Optional: GetArgument<T> throws on a missing argument, so check first.
      var tag = context.ArgumentCount > 1 ? context.GetArgument<string>(1) : null;

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

  // Provides forge.add_cmake(snippet) / forge.add_cmake(snippet, "pre")
  private LuaFunction CreateCustomCMakeFunction() =>
    new("add_cmake", (context, token) =>
    {
      var cmakeSnippet = context.GetArgument<string>(0);
      // Optional: GetArgument<T> throws on a missing argument, so check first.
      var phase = context.ArgumentCount > 1 ? context.GetArgument<string>(1) : null;

      // "pre" lands before the project target (toolchain/SDK setup: variables,
      // find_package, add_subdirectory); anything else is emitted after the
      // target and its link line, which is the historical default.
      if (string.Equals(phase, "pre", StringComparison.OrdinalIgnoreCase))
      {
        _context.CustomCmakeSnippetsPre.Add(cmakeSnippet);
        AnsiConsole.MarkupLine($"[green]Added custom CMake snippet[/] [dim](pre)[/]");
      }
      else
      {
        _context.CustomCmakeSnippets.Add(cmakeSnippet);
        AnsiConsole.MarkupLine($"[green]Added custom CMake snippet[/]");
      }

      return ValueTask.FromResult(0);
    });

  // forge.add_section(name, position, content) — a named CMake section placed
  // relative to a built-in one. forge.add_section(name, content) appends.
  private LuaFunction CreateAddSectionFunction() =>
    new("add_section", (context, token) =>
    {
      if (context.ArgumentCount < 2)
      {
        AnsiConsole.MarkupLine(
          "[bold red]Error:[/] forge.add_section needs (name, content) or (name, position, content).");
        return ValueTask.FromResult(0);
      }

      var name = context.GetArgument<string>(0);
      var position = context.ArgumentCount > 2 ? context.GetArgument<string>(1) : "last";
      var content = context.ArgumentCount > 2 ? context.GetArgument<string>(2) : context.GetArgument<string>(1);

      if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content))
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] forge.add_section needs a name and some CMake content.");
        return ValueTask.FromResult(0);
      }

      if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_-]*$"))
      {
        AnsiConsole.MarkupLine(
          $"[bold red]Error:[/] forge.add_section name `{name}` must start with a letter or `_` " +
          "and contain only letters, digits, `_` or `-`.");
        return ValueTask.FromResult(0);
      }

      _context.RegisterLuaSection(new LuaCmakeSection
      {
        Name = name,
        Position = position,
        Content = content
      });

      AnsiConsole.MarkupLine($"[green]Added CMake section[/] [dim]{name} ({position})[/]");
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
        // The docs promise nil for an unknown key: an empty string would be
        // truthy in Lua and silently break `if forge.config.get(k) then` checks.
        context.Return(value is null ? LuaValue.Nil : new LuaValue(value));
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

        // Options table: { timeout = 300, sha256 = "..." }
        if (options != null && options.TryGetValue("timeout", out var timeoutText) &&
            int.TryParse(timeoutText, out var timeoutSeconds) && timeoutSeconds > 0)
        {
          client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        // Use streaming to avoid memory issues with large files
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long totalRead = 0;

        // The writer is scoped so the file is closed before hashing.
        await using (var contentStream = await response.Content.ReadAsStreamAsync(token))
        await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
          var buffer = new byte[8192];
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
        }

        // Optional verification, matching `forge download --sha-256`.
        if (options != null && options.TryGetValue("sha256", out var expected) &&
            !string.IsNullOrWhiteSpace(expected))
        {
          using var readStream = File.OpenRead(output);
          using var sha = System.Security.Cryptography.SHA256.Create();
          var hash = Convert.ToHexString(await sha.ComputeHashAsync(readStream, token)).ToLowerInvariant();
          if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase))
          {
            AnsiConsole.MarkupLine(
              $"[red]SHA256 verification failed! Expected: {expected}, Got: {hash}[/]");
            File.Delete(output);
            return 1;
          }
          AnsiConsole.MarkupLine("[green]SHA256 verification passed[/]");
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
        // Optional (default 0): GetArgument<T> throws on a missing argument.
        var stripComponents = context.ArgumentCount > 2 ? context.GetArgument<int>(2) : 0;

        // One implementation shared with `forge extract` / `forge fetch`.
        var result = ArchiveExtractor.Extract(archive, output, stripComponents);
        if (result == 0)
        {
          AnsiConsole.MarkupLine($"[green]Extracted:[/] {output}");
          return ValueTask.FromResult(0);
        }

        return ValueTask.FromResult(result);
      });

  // forge.fetch(url, output_dir?) - download and extract to external/<name>
  // Returns the path to the extracted directory
  // forge.fetch(url, output_dir?, strip_components?) - download and extract.
  // Returns the path to the extracted directory.
  private static LuaFunction CreateFetchFunction() =>
      new("fetch", async (context, token) =>
      {
        var url = context.GetArgument<string>(0);
        string? outputOverride = null;
        // Optional: GetArgument<T> throws on a missing argument, so check first.
        if (context.ArgumentCount > 1) outputOverride = context.GetArgument<string>(1);
        var stripComponents = context.ArgumentCount > 2 ? context.GetArgument<int>(2) : 1;

        // Derive a directory name from the URL (like pull_repo does).
        var urlParts = url.Split('/');
        var lastPart = urlParts[^1];
        var archiveName = lastPart.Split('.')[0];
        if (archiveName.StartsWith("archive") || archiveName.StartsWith("refs"))
        {
          archiveName = urlParts.Length > 1 ? urlParts[^2] : archiveName;
        }

        // Default to external/<name> if no output is specified.
        var output = outputOverride ?? Path.Combine("external", archiveName);
        var extension = Path.GetExtension(lastPart);
        var tempFile = Path.Combine(Path.GetTempPath(), $"forge_fetch_{Guid.NewGuid()}{extension}");

        try
        {
          using var client = new HttpClient();
          client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");
          AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {url}");
          var bytes = await client.GetByteArrayAsync(url, token);
          await File.WriteAllBytesAsync(tempFile, bytes, token);

          // One extractor shared with `forge fetch` and forge.extract.
          var result = ArchiveExtractor.Extract(tempFile, output, stripComponents);
          if (File.Exists(tempFile)) File.Delete(tempFile);
          if (result != 0) return 1;

          AnsiConsole.MarkupLine($"[green]Fetch complete:[/] {output}");
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
