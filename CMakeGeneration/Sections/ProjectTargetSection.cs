using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class ProjectTargetSection : CMakeSectionBase
{
  public override string Name => "project_target";

  public override int Priority => 30;

  public override string Generate(ProjectConfig config)
  {
    var sb = new StringBuilder();
    var linkage = config.Project.Linkage.ToUpper() ?? "STATIC";

    sb.AppendLine("# --- Project Target ---");
    sb.AppendLine($"file(GLOB_RECURSE SOURCES RELATIVE ${{PROJECT_SOURCE_DIR}} ${{PROJECT_SOURCE_DIR}}/src/*.cpp)");

    if (config.Project.Type == "executable")
    {
      sb.AppendLine($"add_executable({config.Project.Name} ${{SOURCES}})");
    }
    else if (config.Project.Type == "library")
    {
      sb.AppendLine($"add_library({config.Project.Name} {linkage} ${{SOURCES}})");

      sb.AppendLine($"install(TARGETS {config.Project.Name} EXPORT {config.Project.Name}Config DESTINATION lib)");

      if (config.Project.InstallHeaders)
      {
        sb.AppendLine($"target_include_directories({config.Project.Name} PUBLIC ${{PROJECT_SOURCE_DIR}}/include)");
        sb.AppendLine($"target_include_directories({config.Project.Name} PRIVATE ${{PROJECT_SOURCE_DIR}}/src)");
        sb.AppendLine($"install(DIRECTORY ${{PROJECT_SOURCE_DIR}}/include/ DESTINATION include)");
      }
    }

    return sb.ToString();
  }
}
