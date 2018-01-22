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

        protected override string GetTargetPath(RunningDeployment deployment)
        {
            var metadata = PackageName.FromFile(deployment.PackageFilePath);
            return ApplicationDirectory.GetApplicationDirectory(metadata, deployment.Variables, fileSystem);
        }

    }
}