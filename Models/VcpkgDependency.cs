namespace forge.Models
{
  /// <summary>
  /// A dependency resolved by vcpkg's manifest mode.
  /// </summary>
  public class VcpkgDependency
  {
    /// <summary>The CMake target to link (e.g. <c>fmt::fmt</c>).</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Minimum version, written as <c>"version&gt;="</c> in vcpkg.json.</summary>
    public string Version { get; set; } = string.Empty;
  }
}
