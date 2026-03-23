using forge.Models;

namespace forge.CMakeGeneration
{
    /// <summary>
    /// Base class for CMake sections with common functionality
    /// </summary>
    public abstract class CMakeSectionBase : ICMakeSection
    {
        /// <summary>
        /// Unique identifier for this section
        /// </summary>
        public abstract string Name { get; }
        
        /// <summary>
        /// Priority determines generation order (lower = earlier)
        /// </summary>
        public abstract int Priority { get; }
        
        /// <summary>
        /// Whether this section should be generated (default: true)
        /// </summary>
        public virtual bool IsEnabled(ProjectConfig config) => true;
        
        /// <summary>
        /// Generate CMake content
        /// </summary>
        public abstract string Generate(ProjectConfig config);
    }
}
