using System;

namespace Calamari.Tests.Shared.LogParser
{
    public class CollectedArtifact
    {
        public CollectedArtifact(string name, string? path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }

        public string? Path { get; }

        public long Length { get; set; }
    }
}