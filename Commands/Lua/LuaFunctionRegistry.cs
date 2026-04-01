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

      var repoName = repoUrl.Split('/')[^1].Split(".")[0];

      var processStartInfo = new ProcessStartInfo("git", $"clone {repoUrl} external/{repoName}")
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

      AnsiConsole.MarkupLine($"[green]Added custome CMake snippet[/]");

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

  // forge.download(url, output_path)
  private static LuaFunction CreateDownloadFunction() =>
      new("download", async (context, token) =>
      {
        var url = context.GetArgument<string>(0);
        var output = context.GetArgument<string>(1);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

        var bytes = await client.GetByteArrayAsync(url, token);
        await File.WriteAllBytesAsync(output, bytes, token);

        AnsiConsole.MarkupLine($"[green]Downloaded:[/] {output} ({bytes.Length} bytes)");

        return 0;
      });

  // forge.extract(archive_path, output_dir, string_components?)
  private static LuaFunction CreateExtractFunction() =>
      new("extract", (context, token) =>
      {
        var archive = context.GetArgument<string>(0);
        var output = context.GetArgument<string>(1);
        var stripComponents = context.GetArgument<string>(2);

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

  // forge.fetch(url, output_dir) - download + extract in one step
  private static LuaFunction CreateFetchFunction() =>
      new("fetch", async (context, token) =>
      {
        var url = context.GetArgument<string>(0);
        var output = context.GetArgument<string>(1);
        var stripComponents = context.GetArgument<string>(2); // default 1 

        var tempFile = Path.Combine(Path.GetTempPath(), $"forge_fetch_{Guid.NewGuid()}.zip");

        try
        {
          // Download
          using var client = new HttpClient();
          client.DefaultRequestHeaders.Add("User-Agent", "Forge/1.0");

          AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {url}");
          var bytes = await client.GetByteArrayAsync(url, token);
          await File.WriteAllBytesAsync(tempFile, bytes, token);

          // Extract
          Directory.CreateDirectory(output);
          ZipFile.ExtractToDirectory(tempFile, output, true);

          File.Delete(tempFile);

          AnsiConsole.MarkupLine($"[green]Fetch complete:[/] {output}");
        }
        catch (Exception ex)
        {
          if (File.Exists(tempFile)) File.Delete(tempFile);
          AnsiConsole.MarkupLine($"[red]Fetch failed:[/] {ex.Message}");
          return 1;
        }

        return 0;
      });
}
