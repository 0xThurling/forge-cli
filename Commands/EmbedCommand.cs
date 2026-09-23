using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Registers a resource file to be embedded in the compiled executable.
  /// </summary>
  /// <remarks>
  /// This command adds a file to the [resources] section of forge.lua.
  /// During the next build, the file will be converted to a C++ byte array
  /// and embedded in the executable, allowing runtime access via the
  /// Embedded::get() API.
  /// </remarks>
  /// <example>
  /// <code>
  /// // Embed an image
  /// forge embed assets/icon.png
  /// 
  /// // Embed a shader
  /// forge embed assets/shaders/basic.glsl
  /// </code>
  /// </example>
  [CliCommand(Name = "embed", Description = "Embed a resource file into a C++ header.", Parent = typeof(RootCommand))]
  public class EmbedCommand
  {
    /// <summary>
    /// Gets or sets the path to the resource file to embed.
    /// </summary>
    /// <value>
    /// A relative or absolute path to the file to embed. The file must exist.
    /// </value>
    [CliArgument(Description = "The path to the resource file to embed.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Registers the resource file in forge.lua.
    /// </summary>
    /// <returns>0 on success, 1 when the file or the configuration is missing.</returns>
    public async Task<int> RunAsync()
    {
      if (!File.Exists(FilePath))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] File not found at '[bold]{FilePath}[/]'.");
        return 1;
      }

      AnsiConsole.MarkupLine($"[bold cyan]--- Registering resource: {FilePath} ---[/]");

      var config = await ProjectConfigManager.LoadConfigAsync();
      if (config == null)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] `forge.lua` not found.");
        return 1;
      }

      // Use relative path for portability
      var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), FilePath);

      // The build embeds from the project root, so a path outside it would make
      // the project depend on a sibling file (and break on another machine).
      if (relativePath.StartsWith("..", StringComparison.Ordinal) ||
          Path.IsPathRooted(relativePath))
      {
        AnsiConsole.MarkupLine(
          $"[bold red]Error:[/] `{FilePath}` is outside the project. " +
          "Move it inside the project directory before embedding it.");
        return 1;
      }

      if (!config.Resources.Files.Contains(relativePath))
      {
        config.Resources.Files.Add(relativePath);
        try
        {
          ProjectConfigManager.SaveConfig(config);
          AnsiConsole.MarkupLine($"[bold green]Successfully registered `[bold]{relativePath}[/]` in forge.lua.[/]");
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[bold red]Error:[/] Could not write to forge.lua: {ex.Message}");
          return 1;
        }
      }
      else
      {
        AnsiConsole.MarkupLine($"[yellow]Resource `[bold]{relativePath}[/]` is already registered in forge.lua.[/]");
      }

      return 0;
    }
  }
}
