using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class ConanSection : CMakeSectionBase
{
  public override string Name => "conan";

  public override int Priority => 5;

  public override bool IsEnabled(BuildContext context)
      => context.FindDependencies.Count != 0;

  public override string Generate(BuildContext context)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# --- Depedencies (Conan) ---");

    foreach (var dep in context.FindDependencies)
      sb.AppendLine($"find_package({dep} REQUIRED)");

    return sb.ToString();
  }
}
