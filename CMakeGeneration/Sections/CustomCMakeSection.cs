using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class CustomCMakeSection : CMakeSectionBase
{
    public override string Name => "custom";

    public override int Priority => 45;

    public override bool IsEnabled(ProjectConfig config) =>
      ProjectBuildManager.CustomCmakeSnippets.Count != 0;

    public override string Generate(ProjectConfig config)
    {
      var sb = new StringBuilder();
      sb.AppendLine("# --- Custome CMake (from build scripts) ---");

      foreach (var snippet in ProjectBuildManager.CustomCmakeSnippets) {
        var resolved = snippet.Replace("${PROJECT_NAME}", config.Project.Name);
        sb.AppendLine(resolved);
      }

      return sb.ToString();
    }
}
