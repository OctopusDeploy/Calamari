using System;

namespace Calamari.Extensibility
{
    public interface IFeatureLocator
    {
        Type Locate(string name);
    }
}