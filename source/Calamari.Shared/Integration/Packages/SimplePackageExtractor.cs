using System;
using System.Linq;

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }


        readonly string[] ExcludePaths =
        {
            "octopus.metadata"
        };

        public bool IsExcludedPath(string path)
        {
            return ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    
        }
    }
}