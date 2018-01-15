using System.IO;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Octopus.Versioning;
using Octopus.Versioning.Constants;
using Octopus.Versioning.Metadata;

namespace Calamari.Java.Deployment.Conventions
{
    public class RePackArchiveConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageExtractor packageExtractor;
        readonly JarTool jarTool;

        public RePackArchiveConvention(
            ICalamariFileSystem fileSystem,
            ICommandOutput commandOutput,
            IPackageExtractor packageExtractor,
            ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.packageExtractor = packageExtractor;
            this.jarTool = new JarTool(commandLineRunner, commandOutput, fileSystem);
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

            var packageMetadata = packageExtractor.GetMetadata(deployment.PackageFilePath);

            var repackedArchivePath = CreateArchive(deployment, packageMetadata);

            var repackedArchiveDirectory = Path.GetDirectoryName(repackedArchivePath);

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, repackedArchiveDirectory);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, repackedArchiveDirectory,
                deployment.Variables);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationPackagePath, repackedArchivePath,
                deployment.Variables);
        }

        protected string CreateArchive(RunningDeployment deployment, PackageMetadata packageMetadata)
        {
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(
                packageMetadata,
                deployment.Variables,
                fileSystem);

            var customPackageFileName = deployment.Variables.Get(SpecialVariables.Package.CustomPackageFileName);

            if (!string.IsNullOrWhiteSpace(customPackageFileName))
            {
                Log.Verbose($"Using custom package file-name: '{customPackageFileName}'");
            }

            var targetFileName = !string.IsNullOrWhiteSpace(customPackageFileName)
                ? customPackageFileName
                : new StringBuilder()
                    .Append(packageMetadata.PackageId)
                    /*
                     * If this package uses the maven version format, we use the # char as a delimiter between
                     * the package id and the version. If it is not a maven version, we use the default of
                     * a period.
                     */
                    .Append(packageMetadata.VersionFormat == VersionFormat.Maven ? JavaConstants.MavenFilenameDelimiter : '.')
                    .Append(packageMetadata.Version)
                    .Append(packageMetadata.FileExtension)
                    .ToString();

            var targetFilePath = Path.Combine(applicationDirectory, targetFileName);

            var stagingDirectory = deployment.CurrentDirectory;

            jarTool.CreateJar(stagingDirectory, targetFilePath);
            Log.Info($"Re-packaging archive: '{targetFilePath}'");

            return targetFilePath;
        }
    }
}