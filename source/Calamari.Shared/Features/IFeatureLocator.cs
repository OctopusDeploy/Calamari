using System;
using Calamari.Shared.Convention;

namespace Calamari.Shared.Features
{
    public interface IFeatureLocator
    {
        IFeature ConstructFeature(string name);
        Type GetFeatureType(string name);
    }

    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class CalamariConstructorAttribute : Attribute{ }
}