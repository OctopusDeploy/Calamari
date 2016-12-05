using System;
using System;
using System.Reflection;

namespace Calamari.Extensibility.Features
{
    public class FeatureAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public Type Module { get; set; }
        public FeatureAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}