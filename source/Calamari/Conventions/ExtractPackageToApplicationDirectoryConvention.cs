using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Octostache;

namespace Calamari.Conventions
{
    public class ExtractPackageToApplicationDirectoryConvention : IInstallConvention
    {
        readonly IPackageExtractor extractor;
        readonly ICalamariFileSystem fileSystem;

        public ExtractPackageToApplicationDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var metadata = extractor.GetMetadata(deployment.PackageFilePath);
            var root = GetInitialExtractionDirectory(deployment.Variables);
            var targetPath = Path.Combine(root, metadata.Id, metadata.Version);

            targetPath = EnsureTargetPathIsEmpty(targetPath);

            CalamariLogger.Verbose("Extracting package to: " + targetPath);

            int filesExtracted;
            extractor.Install(deployment.PackageFilePath, targetPath, false, out filesExtracted);

            CalamariLogger.Verbose("Extracted " + filesExtracted + " files");

            deployment.Variables.Set("OctopusOriginalPackageDirectoryPath", targetPath);
        }

        string EnsureTargetPathIsEmpty(string desiredTargetPath)
        {
            var target = desiredTargetPath;

            for (var i = 1; fileSystem.DirectoryExists(target) || fileSystem.FileExists(target); i++)
            {
                target = desiredTargetPath + "_" + i;
            }

            return target;
        }

        string GetInitialExtractionDirectory(VariableDictionary variables)
        {
            var root = variables.Get("Octopus.Tentacle.Agent.ApplicationDirectoryPath")
                ?? variables.Get("env:Octopus.Tentacle.Agent.ApplicationDirectoryPath")
                ?? variables.Evaluate("#{env:SystemDrive}\\Applications");

            var environment = variables.Get("Octopus.Environment.Name");
            if (!string.IsNullOrWhiteSpace(environment))
            {
                root = Path.Combine(root, environment);
            }

            fileSystem.EnsureDirectoryExists(root);

            return root;
        }
    }
}