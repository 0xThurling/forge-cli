using forge.Models;

namespace forge.CMakeGeneration
{
    /// <summary>
    /// Interface for CMake generation sections.
    /// Implement this to add new CMake generation capabilities.
    /// </summary>
    public interface ICMakeSection
    {
        /// <summary>
        /// Unique identifier for this section
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Priority determines generation order (lower = earlier)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Whether this section should be generated
        /// </summary>
        bool IsEnabled(ProjectConfig config);
        
        /// <summary>
        /// Generate CMake content
        /// </summary>
        string Generate(ProjectConfig config);
    }
}
