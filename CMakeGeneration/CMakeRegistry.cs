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
}
