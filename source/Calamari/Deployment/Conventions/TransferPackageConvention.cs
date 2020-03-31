using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public class TransferPackageConvention :IInstallConvention
    {
        readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;

        public TransferPackageConvention(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var transferPath = CrossPlatform.ExpandPathEnvironmentVariables(deployment.Variables.Get(SpecialVariables.Package.TransferPath));
            fileSystem.EnsureDirectoryExists(transferPath);
            var fileName = deployment.Variables.Get(SpecialVariables.Package.OriginalFileName) ?? Path.GetFileName(deployment.PackageFilePath);
            var filePath = Path.Combine(transferPath, fileName);

            if (fileSystem.FileExists(filePath))
            {
                log.Info($"File {filePath} already exists so it will be attempted to be overwritten");
            }

            fileSystem.CopyFile(deployment.PackageFilePath, filePath);

           log.Info($"Copied package '{fileName}' to directory '{transferPath}'");
           log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Package.Output.DirectoryPath, transferPath);
           log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Package.Output.FileName, fileName);
           log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Package.Output.FilePath, filePath);
        }
    }
}