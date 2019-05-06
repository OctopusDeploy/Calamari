﻿using System.IO;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.Java;

namespace Calamari.Java.Deployment.Conventions
{
    public class RePackArchiveConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly JarTool jarTool;

        public RePackArchiveConvention(
            ICalamariFileSystem fileSystem,
            JarTool jarTool)
        {
            this.fileSystem = fileSystem;
            this.jarTool = jarTool;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.JavaArchiveExtractionDisabled))
            {
                Log.Verbose(
                    $"'{SpecialVariables.Action.Java.JavaArchiveExtractionDisabled}' is set. Skipping re-pack.");
                return;
            }

            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.DeployExploded))
            {
                Log.Verbose($"'{SpecialVariables.Action.Java.DeployExploded}' is set. Skipping re-pack.");
                return;
            }

            var repackedArchivePath = CreateArchive(deployment);

            var repackedArchiveDirectory = Path.GetDirectoryName(repackedArchivePath);

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, repackedArchiveDirectory);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, repackedArchiveDirectory, deployment.Variables);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationPackagePath, repackedArchivePath, deployment.Variables);
        }

        protected string CreateArchive(RunningDeployment deployment)
        {
            var packageMetadata = PackageName.FromFile(deployment.PackageFilePath);
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(
                packageMetadata,
                deployment.Variables,
                fileSystem);

            var customPackageFileName = deployment.Variables.Get(SpecialVariables.Package.CustomPackageFileName);

            if (!string.IsNullOrWhiteSpace(customPackageFileName))
            {
                Log.Verbose($"Using custom package file-name: '{customPackageFileName}'");
            }

            var targetFilePath = Path.Combine(applicationDirectory, customPackageFileName ?? Path.GetFileName(deployment.PackageFilePath));

            var stagingDirectory = deployment.CurrentDirectory;

            jarTool.CreateJar(stagingDirectory, targetFilePath);
            Log.Info($"Re-packaging archive: '{targetFilePath}'");

            return targetFilePath;
        }
    }
}