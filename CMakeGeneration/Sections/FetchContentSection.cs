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

      if (string.IsNullOrEmpty(details.Git) || string.IsNullOrEmpty(details.Tag))
      {
        AnsiConsole.Markup($"[yellow]Warning[/]: Skipping invalid dependency '{name}'");
        continue;
      }

      sb.AppendLine($"FetchContent_Declare({name} GIT REPOSITORY \"{details.Git}\" GIT_TAG \"{details.Tag}\")");
      sb.AppendLine($"FetchContent_MakeAvailable({name})");
    }

    return sb.ToString();
  }
}
