using System;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Util;
using Octopus.Versioning.Metadata;

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
            if (string.IsNullOrWhiteSpace(deployment.PackageFilePath))
            {
               Log.Verbose("No package path defined. Skipping package extraction.");
               return;
            }

            try
            {
                var metadata = extractor.GetMetadata(deployment.PackageFilePath);

                var targetPath = GetTargetPath(deployment, metadata);

                Log.Verbose("Extracting package to: " + targetPath);

                var filesExtracted = extractor.Extract(deployment.PackageFilePath, targetPath,
                    deployment.Variables.GetFlag(SpecialVariables.Package.SuppressNestedScriptWarning, false));

                Log.Verbose("Extracted " + filesExtracted + " files");

                deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
                Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath,
                    deployment.Variables);
                Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, targetPath,
                    deployment.Variables);
                Log.SetOutputVariable(SpecialVariables.Package.Output.ExtractedFileCount, filesExtracted.ToString(),
                    deployment.Variables);
            }
            catch (UnauthorizedAccessException)
            {
                LogAccessDenied();
                throw;
            }
            catch (Exception ex) when (ex.Message.ContainsIgnoreCase("Access is denied"))
            {
                LogAccessDenied();
                throw;
            }
        }

        void LogAccessDenied()
        {
            Log.Error("Failed to extract the package because access to the package was denied. This may have happened because anti-virus software is scanning the file. Try disabling your anti-virus software in order to rule this out.");
        }

        protected abstract string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata);

    }
}