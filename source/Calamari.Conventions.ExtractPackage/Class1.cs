using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.Conventions.ExtractPackage
{
    public class RunScriptMetaData : IFeatureHandler
    {
        public string Name { get; } = "RunScript";
        public string Description { get; } = "Allows you to run a script";
        public IEnumerable<string> ConventionDependencies { get; } = new[] {CommonConventions.PackageExtraction};

        public Type Feature { get; } //= typeof(RunScriptFeature);
    }
}