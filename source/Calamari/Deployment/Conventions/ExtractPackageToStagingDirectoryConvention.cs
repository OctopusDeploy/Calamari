using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToStagingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var packageExtractionPathVariable = deployment.Variables[SpecialVariables.Action.Azure.PackageExtractionPath];

            // The PackageExtractionPath variable will always be provided by the OD server, but just in case Calamari is run
            // stand-alone, we will fall-back to a temporary path
            return !string.IsNullOrWhiteSpace(packageExtractionPathVariable)
                ? packageExtractionPathVariable
                : fileSystem.CreateTemporaryDirectory();
        }
    }
}