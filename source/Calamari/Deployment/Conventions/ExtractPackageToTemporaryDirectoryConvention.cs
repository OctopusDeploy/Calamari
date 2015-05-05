using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToTemporaryDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToTemporaryDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            return fileSystem.CreateTemporaryDirectory();
        }
    }
}