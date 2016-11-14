using System;
using System.Collections.Generic;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.RunScript
{
    public class RunScriptMetaData : IFeatureHandler
    {
        public string Name { get; } = "RunScript";
        public string Description { get; } = "Allows you to run a script";
        public IEnumerable<string> ConventionDependencies { get; } = new[] { CommonConventions.PackageExtraction };

        public IFeature CreateFeature()
        {
            throw new NotImplementedException();
            
        }

        public Type Feature { get; } = typeof(RunScriptFeature);
    }
}