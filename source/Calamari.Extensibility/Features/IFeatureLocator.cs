using System;

namespace Calamari.Extensibility.Features
{
    public interface IFeatureLocator
    {
        Type Locate(string name);
    }
}