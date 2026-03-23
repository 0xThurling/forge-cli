using forge.CMakeGeneration.Sections;

namespace forge.CMakeGeneration;

public class CMakeRegistry
{
  private static CMakeRegistry? _instance;

  public static CMakeRegistry Instance => _instance ??= new CMakeRegistry();

  private readonly Dictionary<string, ICMakeSection> _sections = [];

  private bool _initialized = false;

  private CMakeRegistry() {}

  public void Register(ICMakeSection section) {
    _sections[section.Name] = section;
  }

  public void Initialize() {
    if (_initialized) return;

    Register(new StandardSection());
    Register(new FetchContentSection());
    Register(new ConanSection());
    Register(new ProjectTargetSection());
    Register(new LinkingSection());
    Register(new CustomCMakeSection());
    Register(new TestingSection());

    _initialized = true;
  }
}
