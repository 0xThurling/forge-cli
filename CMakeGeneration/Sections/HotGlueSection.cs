using System.Text;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Adds the generated hot-reload glue to the project target.
/// </summary>
/// <remarks>
/// Runs after the project target exists (priority 30) and before linking, so
/// the glue is compiled with the engine's headers, which the linked
/// <c>jet-live</c> target provides.
/// </remarks>
public class HotGlueSection : CMakeSectionBase
{
  public override string Name => "hot_glue";

  public override int Priority => 31;

  public override bool IsEnabled(BuildContext context) => context.Config.Build.Hot;

  public override string Generate(BuildContext context)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# --- Hot reload glue ---");
    sb.AppendLine(
      $"target_sources(${{PROJECT_NAME}} PRIVATE \"${{CMAKE_SOURCE_DIR}}/{HotReload.GlueDirectory}/forge_hot.cpp\")");
    sb.AppendLine(
      $"target_include_directories(${{PROJECT_NAME}} PRIVATE \"${{CMAKE_SOURCE_DIR}}/{HotReload.GlueDirectory}\")");
    return sb.ToString();
  }
}
