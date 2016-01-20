﻿using Calamari.Integration.FileSystem;
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
            if (string.IsNullOrWhiteSpace(deployment.PackageFilePath))
            {
               Log.Verbose("No package path defined. Skipping package extraction.");
               return;
            }

            var metadata = extractor.GetMetadata(deployment.PackageFilePath);

            var targetPath = GetTargetPath(deployment, metadata);

            Log.Verbose("Extracting package to: " + targetPath);

            var filesExtracted = extractor.Extract(deployment.PackageFilePath, targetPath, false);

            Log.Verbose("Extracted " + filesExtracted + " files");

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath, deployment.Variables);
        }

        protected abstract string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata);

    }
}