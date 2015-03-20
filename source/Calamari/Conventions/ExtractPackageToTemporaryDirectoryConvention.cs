using System;
using System.IO;
using Calamari.Integration.Packages;
using Octostache;

namespace Calamari.Conventions
{
    public class ExtractPackageToTemporaryDirectoryConvention : IInstallConvention
    {
        readonly IPackageExtractor extractor;

        public ExtractPackageToTemporaryDirectoryConvention(IPackageExtractor extractor)
        {
            this.extractor = extractor;
        }

        public void Install(RunningDeployment deployment)
        {
            // Get the package file
            // Extract it using System.IO.Packaging
            // Store the result as a variable

            var metadata = extractor.GetMetadata(deployment.PackageFilePath);
            var root = GetInitialExtractionDirectory(deployment.Variables);
            var targetPath = Path.Combine(root, metadata.Id, metadata.Version);

            targetPath = EnsureTargetPathIsEmpty(targetPath);

            CalamariLogger.Verbose("Extracting package to: " + targetPath);

            int filesExtracted;
            extractor.Install(deployment.PackageFilePath, targetPath, false, out filesExtracted);
            CalamariLogger.Verbose("Extracted " + filesExtracted + " files");
        }

        static string EnsureTargetPathIsEmpty(string desiredTargetPath)
        {
            var target = desiredTargetPath;

            for (var i = 1; Directory.Exists(target); i++)
            {
                target = desiredTargetPath + "_" + i;
            }

            return target;
        }

        static string GetInitialExtractionDirectory(VariableDictionary variables)
        {
            var root = variables.Get("Octopus.Tentacle.Agent.ApplicationDirectoryPath")
                ?? variables.Get("env:Octopus.Tentacle.Agent.ApplicationDirectoryPath")
                ?? variables.Evaluate("#{env:SystemDrive}\\Applications");

            var environment = variables.Get("Octopus.Environment.Name");
            if (!string.IsNullOrWhiteSpace(environment))
            {
                root = Path.Combine(root, environment);
            }

            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);

            return root;
        }
    }
}