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

    sb.AppendLine("# --- Project Target ---");
    sb.AppendLine($"file(GLOB_RECURSE SOURCES RELATIVE ${{PROJECT_SOURCE_DIR}} ${{PROJECT_SOURCE_DIR}}/src/*.cpp)");

    if (config.Project.Type == "executable")
    {
      sb.AppendLine($"add_executable({config.Project.Name} ${{SOURCES}})");
    }
    else if (config.Project.Type == "library")
    {
      var linkage = config.Project.Linkage.ToUpper();
      sb.AppendLine($"add_library({config.Project.Name} {linkage} ${{SOURCES}})");

      if (config.Project.InstallHeaders)
      {
        sb.AppendLine($"target_include_directories({config.Project.Name} PUBLIC ${{PROJECT_SOURCE_DIR}}/src)");
        sb.AppendLine($"install(TARGETS {config.Project.Name} EXPORT {config.Project.Name}Config DESTINATION lib)");
        sb.AppendLine($"install(DIRECTORY ${{PROJECT_SOURCE_DIR}}/src/ DESTINATION include/{config.Project.Name})");
      }
    }

    return sb.ToString();
  }
}
