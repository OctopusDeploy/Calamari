using System;
using System.Collections.Generic;

namespace Calamari.Shared.Features
{
    public interface IFeatureHandler
    {
        string Name { get; }
        string Description { get; }

        IEnumerable<string> ConventionDependencies { get; }

        Type Feature { get; }
    }
}