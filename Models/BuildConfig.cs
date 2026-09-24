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

    /// <summary>
    /// Compiler launcher for caching builds (<c>ccache</c>, <c>sccache</c>).
    /// Empty means "auto-detect on PATH"; <c>none</c> disables it.
    /// </summary>
    public string CompilerLauncher { get; set; } = string.Empty;

    /// <summary>C++ compiler to configure with (path or name).</summary>
    public string CxxCompiler { get; set; } = string.Empty;

    /// <summary>C compiler to configure with (path or name).</summary>
    public string CCompiler { get; set; } = string.Empty;

    /// <summary>Explicit CMake toolchain file (cannot be combined with Conan/vcpkg).</summary>
    public string ToolchainFile { get; set; } = string.Empty;

    /// <summary>Extra `CMAKE_PREFIX_PATH` entries, for SDK-style dependencies.</summary>
    public List<string> CmakePrefixPath { get; set; } = [];

    /// <summary>`CMAKE_SYSTEM_NAME`, for cross-compiling (e.g. Linux, Windows).</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>`CMAKE_SYSTEM_PROCESSOR` (e.g. aarch64).</summary>
    public string SystemProcessor { get; set; } = string.Empty;

    /// <summary>CMake generator (e.g. Ninja, "Unix Makefiles").</summary>
    public string Generator { get; set; } = string.Empty;

    /// <summary>Default parallel job count; `--jobs` overrides it.</summary>
    public int Jobs { get; set; }

    /// <summary>
    /// Shared dependency cache: <c>"shared"</c> (default) reuses fetched git
    /// checkouts between projects, <c>"off"</c> fetches per project. The
    /// <c>FORGE_NO_CACHE</c> environment variable disables it too.
    /// </summary>
    public string Cache { get; set; } = "shared";

    /// <summary>Build the project as a single translation unit (`CMAKE_UNITY_BUILD`).</summary>
    public bool Unity { get; set; }

    /// <summary>Precompiled header to apply to the project and test targets.</summary>
    public string Pch { get; set; } = string.Empty;

    /// <summary>Scan sources for C++20 modules (`CMAKE_CXX_SCAN_FOR_MODULES`).</summary>
    public bool Modules { get; set; }

    /// <summary>
    /// Hot reload support: builds with jet-live wired in, so function bodies
    /// can be patched in the running process. Debug builds only; see
    /// <c>forge hot</c>.
    /// </summary>
    public bool Hot { get; set; }

    public bool HasAny =>
        Hot ||
        Unity ||
        Pch.Length > 0 ||
        Modules ||
        CompilerLauncher.Length > 0 ||
        CxxCompiler.Length > 0 ||
        CCompiler.Length > 0 ||
        ToolchainFile.Length > 0 ||
        CmakePrefixPath.Count > 0 ||
        SystemName.Length > 0 ||
        SystemProcessor.Length > 0 ||
        Generator.Length > 0 ||
        Jobs > 0 ||
        Presets.Count > 0 ||
        CompileOptions.Count > 0 ||
        CompileDefinitions.Count > 0 ||
        LinkOptions.Count > 0 ||
        LinkLibraries.Count > 0;
  }
}
