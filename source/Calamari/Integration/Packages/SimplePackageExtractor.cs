using Octopus.Core.Resources;
using Octopus.Core.Resources.Metadata;

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public virtual PackageMetadata GetMetadata(string packageFile)
        {
            return new NuGetPackageIDParser().GetMetadataFromPackageName(packageFile, Extensions);
        }

        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }       
    }
}