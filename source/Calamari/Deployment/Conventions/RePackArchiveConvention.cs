using System.IO;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Octopus.Versioning;
using IConvention = Calamari.Deployment.Conventions.IConvention;

namespace Calamari.Java.Deployment.Conventions
{
    public class RePackArchiveConvention : Shared.Commands.IConvention
    {
        readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;
        readonly JarTool jarTool;

        public RePackArchiveConvention(
            ICalamariFileSystem fileSystem,
            ICommandOutput commandOutput,
            ICommandLineRunner commandLineRunner,
            ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.jarTool = new JarTool(commandLineRunner, commandOutput, fileSystem);
        }

        public void Run(IExecutionContext deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.JavaArchiveExtractionDisabled))
            {
                log.Verbose(
                    $"'{SpecialVariables.Action.Java.JavaArchiveExtractionDisabled}' is set. Skipping re-pack.");
                return;
            }

            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.DeployExploded))
            {
                log.Verbose($"'{SpecialVariables.Action.Java.DeployExploded}' is set. Skipping re-pack.");
                return;
            }

            var repackedArchivePath = CreateArchive(deployment);

            var repackedArchiveDirectory = Path.GetDirectoryName(repackedArchivePath);

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, repackedArchiveDirectory);
            log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, repackedArchiveDirectory, deployment.Variables);
            log.SetOutputVariable(SpecialVariables.Package.Output.InstallationPackagePath, repackedArchivePath, deployment.Variables);
        }

        protected string CreateArchive(IExecutionContext deployment)
        {
            var packageMetadata = PackageName.FromFile(deployment.PackageFilePath);
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(
                packageMetadata,
                deployment.Variables,
                fileSystem);

            var customPackageFileName = deployment.Variables.Get(SpecialVariables.Package.CustomPackageFileName);

            if (!string.IsNullOrWhiteSpace(customPackageFileName))
            {
                log.Verbose($"Using custom package file-name: '{customPackageFileName}'");
            }

            var targetFilePath = Path.Combine(applicationDirectory, customPackageFileName ?? Path.GetFileName(deployment.PackageFilePath));

            var stagingDirectory = deployment.CurrentDirectory;

            jarTool.CreateJar(stagingDirectory, targetFilePath);
            log.Info($"Re-packaging archive: '{targetFilePath}'");

            return targetFilePath;
        }
    }
}