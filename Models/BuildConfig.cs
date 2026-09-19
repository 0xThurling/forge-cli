namespace forge.Models
{
  /// <summary>
  /// Compiler/linker configuration from the <c>build</c> section of forge.lua.
  /// </summary>
  /// <remarks>
  /// <c>Presets</c> are named, cross-platform flag bundles expanded by Forge
  /// (e.g. <c>production</c>, <c>concurrency</c>, <c>simd</c>). The raw lists are
  /// verbatim escape hatches for anything a preset does not cover.
  /// </remarks>
  public class BuildConfig
  {
    /// <summary>Named flag bundles, e.g. <c>{ "production", "concurrency", "simd" }</c>.</summary>
    public List<string> Presets { get; set; } = [];

    /// <summary>Extra compiler options, passed through verbatim.</summary>
    public List<string> CompileOptions { get; set; } = [];

    /// <summary>Extra preprocessor definitions (e.g. <c>FOO=1</c>).</summary>
    public List<string> CompileDefinitions { get; set; } = [];

    /// <summary>Extra linker options, passed through verbatim.</summary>
    public List<string> LinkOptions { get; set; } = [];

    /// <summary>Extra libraries to link.</summary>
    public List<string> LinkLibraries { get; set; } = [];

    public bool HasAny =>
      Presets.Count > 0 ||
      CompileOptions.Count > 0 ||
      CompileDefinitions.Count > 0 ||
      LinkOptions.Count > 0 ||
      LinkLibraries.Count > 0;
  }
}
