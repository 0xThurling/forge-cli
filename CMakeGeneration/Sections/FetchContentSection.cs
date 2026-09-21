using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge.CMakeGeneration.Sections;

public class FetchContentSection : CMakeSectionBase
{
  public override string Name => "fetchcontent";

  public override int Priority => 10;

  public override bool IsEnabled(ProjectConfig config) =>
    config.Dependencies.Count != 0;

  public override string Generate(ProjectConfig config)
  {
    var sb = new StringBuilder();

    sb.AppendLine("include(FetchContent)");
    sb.AppendLine("");
    sb.AppendLine("# --- Dependencies ---");

    foreach (var dep in config.Dependencies)
    {
      var name = dep.Key;
      var details = dep.Value;

      // A local checkout: declare the directory as the source and skip the
      // download entirely. Emitted relative to the project so the generated
      // file stays portable across machines that share the layout.
      if (!string.IsNullOrWhiteSpace(details.Path))
      {
        var local = details.Path.Trim();
        var absolute = System.IO.Path.IsPathRooted(local)
          ? local
          : System.IO.Path.GetFullPath(local, Directory.GetCurrentDirectory());

        if (!Directory.Exists(absolute))
        {
          AnsiConsole.MarkupLine(
            $"[bold yellow]Warning:[/] Dependency '{name}' points at '{local}', which does not exist (expected '{absolute}').");
        }

        var emitted = System.IO.Path.IsPathRooted(local)
          ? local.Replace('\\', '/')
          : "${CMAKE_CURRENT_SOURCE_DIR}/" + local.Replace('\\', '/');

        sb.AppendLine($"FetchContent_Declare({name} SOURCE_DIR \"{emitted}\")");
        sb.AppendLine($"FetchContent_MakeAvailable({name})");
        continue;
      }

      if (string.IsNullOrEmpty(details.Git) || string.IsNullOrEmpty(details.Tag))
      {
        AnsiConsole.Markup($"[yellow]Warning[/]: Skipping invalid dependency '{name}'");
        continue;
      }

      sb.AppendLine($"FetchContent_Declare({name} GIT_REPOSITORY \"{details.Git}\" GIT_TAG \"{details.Tag}\")");
      sb.AppendLine($"FetchContent_MakeAvailable({name})");
    }

    return sb.ToString();
  }
}
