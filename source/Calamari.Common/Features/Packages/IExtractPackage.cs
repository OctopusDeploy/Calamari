using System;
using Calamari.Common.Plumbing.Deployment;

namespace Calamari.Common.Features.Packages
{
    public interface IExtractPackage
    {
        void ExtractToStagingDirectory(PathToPackage? pathToPackage, IPackageExtractor? customPackageExtractor = null, string? workingDirectory = null);
        void ExtractToStagingDirectory(PathToPackage? pathToPackage, string extractedToPathOutputVariableName, string? workingDirectory = null);
        void ExtractToEnvironmentCurrentDirectory(PathToPackage pathToPackage);
        void ExtractToApplicationDirectory(PathToPackage pathToPackage, IPackageExtractor? customPackageExtractor = null);
    }
}