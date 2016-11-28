using System;

namespace Calamari.Extensibility.Features
{
    public class FeatureAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public FeatureAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}