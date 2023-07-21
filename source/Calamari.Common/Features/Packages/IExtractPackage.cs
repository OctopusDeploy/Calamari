using System;
using Calamari.Common.Plumbing.Deployment;

namespace Calamari.Common.Features.Packages
{
    public interface IExtractPackage
    {
        void ExtractToCustomDirectory(PathToPackage? pathToPackage, string directory);
        void ExtractToStagingDirectory(PathToPackage? pathToPackage, IPackageExtractor? customPackageExtractor = null);
        void ExtractToStagingDirectory(PathToPackage? pathToPackage, string extractedToPathOutputVariableName);
        void ExtractToEnvironmentCurrentDirectory(PathToPackage pathToPackage);
        void ExtractToApplicationDirectory(PathToPackage pathToPackage, IPackageExtractor? customPackageExtractor = null);
    }
}