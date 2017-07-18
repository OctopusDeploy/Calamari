using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public class TransferPackageConvention :IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;

        public TransferPackageConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var transferPath = deployment.Variables.GetEnvironmentExpandedPath(SpecialVariables.Package.TransferPath);
            fileSystem.EnsureDirectoryExists(transferPath);
            var fileName = deployment.Variables.Get(SpecialVariables.Package.OriginalFileName) ?? Path.GetFileName(deployment.PackageFilePath);
            var filePath = Path.Combine(transferPath, fileName);

            if (fileSystem.FileExists(filePath))
            {
                Log.Info($"File {filePath} already exists so it will be attempted to be overwritten");
            }

            fileSystem.CopyFile(deployment.PackageFilePath, filePath);

            Log.Info($"Copied package '{fileName}' to directory '{transferPath}'");
            Log.SetOutputVariable(SpecialVariables.Package.Output.DirectoryPath, transferPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FileName, fileName);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FilePath, filePath);
        }
    }
}