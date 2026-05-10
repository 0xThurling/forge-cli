namespace forge.Models
{
  /// <summary>
  /// Represents the resources section of the forge.lua configuration file.
  /// </summary>
  /// <remarks>
  /// Contains a list of file paths that should be embedded directly into the
  /// compiled executable during the build process. These can be any binary files
  /// such as images, shaders, configuration files, or fonts.
  /// </remarks>
  public class ResourcesSection
  {
    /// <summary>
    /// Gets or sets the list of file paths to embed in the executable.
    /// </summary>
    /// <value>
    /// A list of relative paths from the project root to files that should be embedded.
    /// </value>
    public List<string> Files { get; set; } = [];
  }
}
