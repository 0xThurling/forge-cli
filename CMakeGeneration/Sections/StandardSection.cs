using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class StandardSection(string standard = "20") : CMakeSectionBase
{
    public override string Name => "standard";

    public override int Priority => 1;

    private readonly string _standard = standard;

    public override string Generate(ProjectConfig config)
    {
      return $@"
        set(CMAKE_CXX_STANDARD {_standard})
        set(CMAKE_CXX_STANDARD_REQUIRED ON)
        set(CMAKE_CXX_EXTENSIONS OFF)
      ";
    }
}
