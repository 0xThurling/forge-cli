using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class FetchContentSection : CMakeSectionBase
{
    public override string Name => "fetchcontent";

    public override int Priority => 10;

    public override bool IsEnabled(ProjectConfig config) => 
      config.Dependencies.Count != 0;

    public override string Generate(ProjectConfig config)
    {
      var sb = new StringBuilder();
    }
}
