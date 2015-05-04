using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Deployment.Conventions
{

    public class ExtractPackageToApplicationDirectoryConvention : IInstallConvention
    {
        readonly IPackageExtractor extractor;
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphore semaphore;

        public ExtractPackageToApplicationDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem, ISemaphore semaphore)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
            this.semaphore = semaphore;
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
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath);
        }

        string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var root = GetInitialExtractionDirectory(deployment.Variables);
            return Path.Combine(root, metadata.Id, metadata.Version);
        }

        string GetInitialExtractionDirectory(VariableDictionary variables)
        {
            var root = GetApplicationDirectoryPath(variables);
            root = AppendEnvironmentNameIfProvided(variables, root);
            fileSystem.EnsureDirectoryExists(root);
            fileSystem.EnsureDiskHasEnoughFreeSpace(root);
            return root;
        }

        string GetApplicationDirectoryPath (VariableDictionary variables)
        {
            const string windowsRoot = "env:SystemDrive";
            const string linuxRoot = "env:HOME";

            var root = variables.Get(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath);
            if (root != null)
                return root;

            root = variables.Get(windowsRoot);
            if (root == null)
            {
                root = variables.Get(linuxRoot);
                if (root == null)
                {
                    throw new Exception(string.Format("Unable to determine the ApplicationRootDirectory. Please provide the {0} variable", SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath));
                }
            }
            return string.Format("{0}{1}Applications", root, Path.DirectorySeparatorChar);
        }

        string AppendEnvironmentNameIfProvided(VariableDictionary variables, string root)
        {
            var environment = variables.Get(SpecialVariables.Environment.Name);
            if (!string.IsNullOrWhiteSpace(environment))
            {
                environment = fileSystem.RemoveInvalidFileNameChars(environment);
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

            using (semaphore.Acquire("Octopus.Calamari.ExtractionDirectory", "Another process is finding an extraction directory, please wait..."))
            {
                for (var i = 1; fileSystem.DirectoryExists(target) || fileSystem.FileExists(target); i++)
                {
                    target = desiredTargetPath + "_" + i;
                }

                fileSystem.EnsureDirectoryExists(target);
            }

            return target;
        }
    }
}