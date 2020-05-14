namespace Sashimi.Tests.Shared.LogParser
{
    public class CollectedArtifact
    {
        public CollectedArtifact(string name, string? path)
        {
            this.Name = name;
            this.Path = path;
        }

        public string Name { get; }

        public string? Path { get; }

        public long Length { get; set; }
    }
}