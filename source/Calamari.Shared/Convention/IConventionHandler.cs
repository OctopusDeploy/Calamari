using System;

namespace Calamari.Shared.Convention
{
    public interface IConventionHandler
    {
        string Name { get; }
        string Description { get; }

        IConvention CreateConvention();

        Type ConventionType { get; }
    }

    public class ConventionMetadataAttribute : Attribute
    {
        private bool IsPublic { get; }
        public string Name { get; }
        public string Description { get; }
        
        public ConventionMetadataAttribute(string name, string description, bool isPublic = false)
        {
            IsPublic = isPublic;
            Name = name;
            Description = description;
        }
    }
}