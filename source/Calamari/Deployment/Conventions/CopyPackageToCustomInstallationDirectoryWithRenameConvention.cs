using System;
using System.IO;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class CopyPackageToCustomInstallationDirectoryWithRenameConvention : CopyPackageToCustomInstallationDirectoryConvention
    {
        public CopyPackageToCustomInstallationDirectoryWithRenameConvention(ICalamariFileSystem fileSystem) : base(fileSystem)
        {
        }

        protected override int Copy(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var customPackageFileName = variables.Get(SpecialVariables.Package.CustomPackageFileName);

            // If we are not using a custom package filename, then we can rely on the base implementation
            if (string.IsNullOrWhiteSpace(customPackageFileName))
            {
                return base.Copy(deployment);
            }

            var originalPackageLocation = variables.Get(SpecialVariables.Package.Output.InstallationPackagePath); 
            var targetPath = Path.Combine(deployment.CustomDirectory, customPackageFileName);

            Log.Info($"Copying package to custom installation location '{targetPath}'");

            fileSystem.CopyFile(originalPackageLocation, targetPath);

            return 1;
        }
    }
}