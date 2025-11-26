namespace forge.Models
{
    public class ProjectConfig
    {
        public ProjectSection Project { get; set; } = new();
        public Dictionary<string, Dependency> Dependencies { get; set; } = new();
        public Dictionary<string, string> ConanDependencies {get;} = new();
        public ResourcesSection Resources { get; set; } = new();
        public Dictionary<string, string> Scripts { get; } = new();
    }
}
