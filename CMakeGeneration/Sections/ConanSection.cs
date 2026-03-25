using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class ConanSection : CMakeSectionBase
{
  public override string Name => "conan";

  public override int Priority => 5;

  public override bool IsEnabled(ProjectConfig config)
      => ProjectBuildManager.FindDependencies.Count != 0;

  public override string Generate(ProjectConfig config)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# --- Depedencies (Conan) ---");

    foreach (var dep in ProjectBuildManager.FindDependencies)
      sb.AppendLine($"find_package({dep} REQUIRED)");

    return sb.ToString();
  }
}
