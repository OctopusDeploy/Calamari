using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.FileSystem;
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
            var packagePath = deployment.PackageFilePath;
            if (string.IsNullOrWhiteSpace(packagePath))
            {
               Log.Verbose("No package path defined. Skipping package extraction.");
               return;
            }

            Log.Info("Extracting package: " + packagePath);

            if (!fileSystem.FileExists(packagePath))
                throw new CommandException("Could not find package file: " + packagePath);

            var metadata = extractor.GetMetadata(packagePath);

            var targetPath = GetTargetPath(deployment, metadata);

            Log.Verbose("Extracting package to: " + targetPath);

            var filesExtracted = extractor.Extract(packagePath, targetPath, deployment.Variables.GetFlag(SpecialVariables.Package.SuppressNestedScriptWarning, false));

            Log.Verbose("Extracted " + filesExtracted + " files");

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath, deployment.Variables);
            Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, targetPath, deployment.Variables);
        }

        protected abstract string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata);

    }
}