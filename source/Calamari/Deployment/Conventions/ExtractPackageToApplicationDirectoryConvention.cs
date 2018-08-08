using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToApplicationDirectoryConvention : ExtractPackageConvention 
    {

        public ExtractPackageToApplicationDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(IExecutionContext deployment)
        {
            var metadata = PackageName.FromFile(deployment.PackageFilePath);
            return ApplicationDirectory.GetApplicationDirectory(metadata, deployment.Variables, fileSystem);
        }

    }
}