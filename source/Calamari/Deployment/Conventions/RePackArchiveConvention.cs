using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions
{
    public class RePackArchiveConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly JarTool jarTool;

        public RePackArchiveConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            JarTool jarTool)
        {
            this.log = log;
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

            deployment.Variables.Set(KnownVariables.OriginalPackageDirectoryPath, repackedArchiveDirectory);
            Log.SetOutputVariable(PackageVariables.Output.InstallationDirectoryPath, repackedArchiveDirectory, deployment.Variables);
            Log.SetOutputVariable(PackageVariables.Output.InstallationPackagePath, repackedArchivePath, deployment.Variables);
        }

        protected string CreateArchive(RunningDeployment deployment)
        {
            var packageMetadata = PackageName.FromFile(deployment.PackageFilePath);
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(
                packageMetadata,
                deployment.Variables,
                fileSystem);

            var customPackageFileName = deployment.Variables.Get(PackageVariables.CustomPackageFileName);

            if (!string.IsNullOrWhiteSpace(customPackageFileName))
            {
                Log.Verbose($"Using custom package file-name: '{customPackageFileName}'");
            }

            var targetFilePath = Path.Combine(applicationDirectory, customPackageFileName ?? Path.GetFileName(deployment.PackageFilePath));

            var stagingDirectory = deployment.CurrentDirectory;

            var enableCompression = deployment.Variables.GetFlag(PackageVariables.JavaArchiveCompression, true);

            jarTool.CreateJar(stagingDirectory, targetFilePath, enableCompression);
            log.Info($"Re-packaging archive: '{targetFilePath}'");

            return targetFilePath;
        }
    }
}