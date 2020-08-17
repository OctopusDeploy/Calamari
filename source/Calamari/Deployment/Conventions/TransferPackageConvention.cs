using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

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
            var transferPath = CrossPlatform.ExpandPathEnvironmentVariables(deployment.Variables.Get(PackageVariables.TransferPath));
            fileSystem.EnsureDirectoryExists(transferPath);
            var fileName = deployment.Variables.Get(PackageVariables.OriginalFileName) ?? Path.GetFileName(deployment.PackageFilePath);
            var filePath = Path.Combine(transferPath, fileName);

            if (fileSystem.FileExists(filePath))
            {
                log.Info($"File {filePath} already exists so it will be attempted to be overwritten");
            }

            fileSystem.CopyFile(deployment.PackageFilePath, filePath);

           log.Info($"Copied package '{fileName}' to directory '{transferPath}'");
           log.SetOutputVariableButDoNotAddToVariables(PackageVariables.Output.DirectoryPath, transferPath);
           log.SetOutputVariableButDoNotAddToVariables(PackageVariables.Output.FileName, fileName);
           log.SetOutputVariableButDoNotAddToVariables(PackageVariables.Output.FilePath, filePath);
        }
    }
}