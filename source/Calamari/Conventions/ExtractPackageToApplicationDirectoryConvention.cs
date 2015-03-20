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

            var targetPath = GetTargetPath(deployment, metadata);
            targetPath = EnsureTargetPathIsEmpty(targetPath);

            Log.Verbose("Extracting package to: " + targetPath);

            int filesExtracted;
            extractor.Install(deployment.PackageFilePath, targetPath, false, out filesExtracted);

            Log.Verbose("Extracted " + filesExtracted + " files");

            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
        }

        string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var root = GetInitialExtractionDirectory(deployment.Variables);
            return Path.Combine(root, metadata.Id, metadata.Version);
        }

        string GetInitialExtractionDirectory(VariableDictionary variables)
        {
            var root = variables.Get(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath)
                ?? variables.Get(SpecialVariables.Tentacle.Agent.EnvironmentApplicationDirectoryPath)
                ?? variables.Evaluate("#{env:SystemDrive}\\Applications");

            root = AppendEnvironmentNameIfProvided(variables, root);
            fileSystem.EnsureDirectoryExists(root);
            fileSystem.EnsureDiskHasEnoughFreeSpace(root);
            return root;
        }

        string AppendEnvironmentNameIfProvided(VariableDictionary variables, string root)
        {
            var environment = variables.Get(SpecialVariables.Environment.Name);
            if (!string.IsNullOrWhiteSpace(environment))
            {
                root = Path.Combine(root, environment);
            }
            return root;
        }

        // When a package has been installed once, Octopus gives users the ability to 'force' a redeployment of the package. 
        // This is often useful for example if a deployment partially completes and the installation is in an invalid state 
        // (e.g., corrupt files are left on disk, or the package is only half extracted). We *can't* just uninstall the package 
        // or overwrite the files, since they might be locked by IIS or another process. So instead we create a new unique 
        // directory. 
        string EnsureTargetPathIsEmpty(string desiredTargetPath)
        {
            var target = desiredTargetPath;

            for (var i = 1; fileSystem.DirectoryExists(target) || fileSystem.FileExists(target); i++)
            {
                target = desiredTargetPath + "_" + i;
            }

            return target;
        }
    }
}