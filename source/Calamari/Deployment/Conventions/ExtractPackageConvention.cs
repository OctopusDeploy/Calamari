using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Deployment.Conventions
{
    public abstract class ExtractPackageConvention : IInstallConvention
    {
        readonly IPackageExtractor extractor;
        protected readonly ICalamariFileSystem fileSystem;

        protected ExtractPackageConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var metadata = extractor.GetMetadata(deployment.PackageFilePath);

            var targetPath = GetTargetPath(deployment, metadata);

            Log.Verbose("Extracting package to: " + targetPath);

            int filesExtracted;
            extractor.Install(deployment.PackageFilePath, targetPath, false, out filesExtracted);

            Log.Verbose("Extracted " + filesExtracted + " files");

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath);
        }

        protected abstract string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata);

    }
}