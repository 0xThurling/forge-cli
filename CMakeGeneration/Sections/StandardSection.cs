using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class StandardSection() : CMakeSectionBase
{
  public override string Name => "standard";

  public override int Priority => 1;

  public override string Generate(ProjectConfig config)
  {
    return $@"
set(CMAKE_CXX_STANDARD {config.Project.Standard})
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
      ";
  }
}
