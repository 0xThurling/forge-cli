using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class FeaturesSection : CMakeSectionBase
{
  public override string Name => "features";

  public override int Priority => 2;

  public override bool IsEnabled(ProjectConfig config)
    => config.Features.Any(f => f.Value.Enabled);

  public override string Generate(ProjectConfig config)
  {
    var sb = new StringBuilder();

    sb.AppendLine("# --- Feature Flags ---");

    foreach (var feature in config.Features.Where(f => f.Value.Enabled))
    {
      var featureUpper = feature.Key.ToUpperInvariant();
      sb.AppendLine($"add_compile_definitions(FEATURE_{featureUpper})");

      // Add feature options as defines
      foreach (var option in feature.Value.Options)
      {
        var optionUpper = option.Key.ToUpperInvariant();
        sb.AppendLine($"add_compile_definitions(FEATURE_{featureUpper}_{optionUpper}=\"{option.Value}\")");
      }
    }

    return sb.ToString();
  }
}
