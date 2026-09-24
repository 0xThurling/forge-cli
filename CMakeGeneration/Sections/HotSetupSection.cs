using System.Text;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Wires jet-live's compiler and linker flags into the generated CMake.
/// </summary>
/// <remarks>
/// This has to run after the FetchContent section — the include needs the
/// fetched source directory — and before the project target, because the flags
/// must be set before the target is created to apply to it.
/// </remarks>
public class HotSetupSection : CMakeSectionBase
{
  public override string Name => "hot";

  public override int Priority => 11;

  public override bool IsEnabled(BuildContext context) => context.Config.Build.Hot;

  public override string Generate(BuildContext context)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# --- Hot reload (jet-live) ---");
    sb.AppendLine("include(${jetlive_SOURCE_DIR}/cmake/jet_live_setup.cmake)");
    return sb.ToString();
  }
}
