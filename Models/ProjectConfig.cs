namespace cpm.Models
{
    public partial class ProjectConfig
    {
        public ProjectSection Project { get; set; } = new ProjectSection();
        public Dictionary<string, Dependency> Dependencies { get; set; } = new Dictionary<string, Dependency>();
        public Dictionary<string, string> ConanDependencies {get; set;} = new Dictionary<string, string>();
        public ResourcesSection Resources { get; set; } = new ResourcesSection();
        public Dictionary<string, string> Scripts { get; set; } = new Dictionary<string, string>();
    }
}
