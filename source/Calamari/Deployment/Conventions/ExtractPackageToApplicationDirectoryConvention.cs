using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Octopus.Versioning.Metadata;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToApplicationDirectoryConvention : ExtractPackageConvention 
    {

        public ExtractPackageToApplicationDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            return ApplicationDirectory.GetApplicationDirectory(metadata, deployment.Variables, fileSystem);
        }

    }
}