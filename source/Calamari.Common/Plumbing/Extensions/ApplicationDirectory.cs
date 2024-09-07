using System;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Extensions
{
    public class ApplicationDirectory
    {
        static readonly ISemaphoreFactory Semaphore = new SystemSemaphoreManager();

        /// <summary>
        /// Returns the directory where the package will be installed.
        /// Also ensures the directory exists, and that there is free-space on the disk.
        /// </summary>
        public static string GetApplicationDirectory(PackageFileNameMetadata packageFileNameMetadata, IVariables variables, ICalamariFileSystem fileSystem)
        {
            return EnsureTargetPathExistsAndIsEmpty(
                Path.Combine(GetEnvironmentApplicationDirectory(fileSystem, variables),
                    FileNameEscaper.Escape(packageFileNameMetadata.PackageId),
                    FileNameEscaper.Escape(packageFileNameMetadata.Version.ToString())),
                fileSystem);
        }

        /// This will be specific to Tenant and/or Environment if these variables are available.
        static string GetEnvironmentApplicationDirectory(ICalamariFileSystem fileSystem, IVariables variables)
        {
            var root = GetApplicationDirectoryRoot(variables);
            root = AppendTenantNameIfProvided(fileSystem, variables, root);
            root = AppendEnvironmentNameIfProvided(fileSystem, variables, root);

            fileSystem.EnsureDirectoryExists(root);
            new FreeSpaceChecker(fileSystem, variables).EnsureDiskHasEnoughFreeSpace(root);

            return root;
        }

        static string GetApplicationDirectoryRoot(IVariables variables)
        {
            const string windowsRoot = "env:SystemDrive";
            const string linuxRoot = "env:HOME";

            var root = variables.Get(TentacleVariables.Agent.ApplicationDirectoryPath);
            if (root != null)
                return root;

            root = variables.Get(windowsRoot);
            if (root == null)
            {
                root = variables.Get(linuxRoot);
                if (root == null)
                    throw new Exception(string.Format("Unable to determine the ApplicationRootDirectory. Please provide the {0} variable", TentacleVariables.Agent.ApplicationDirectoryPath));
            }

            return string.Format("{0}{1}Applications", root, Path.DirectorySeparatorChar);
        }

        static string AppendEnvironmentNameIfProvided(ICalamariFileSystem fileSystem, IVariables variables, string root)
        {
            var environment = variables.Get(DeploymentEnvironment.Name);
            if (!string.IsNullOrWhiteSpace(environment))
            {
                environment = fileSystem.RemoveInvalidFileNameChars(environment);
                root = Path.Combine(root, environment);
            }

            return root;
        }

        static string AppendTenantNameIfProvided(ICalamariFileSystem fileSystem, IVariables variables, string root)
        {
            var tenant = variables.Get(DeploymentVariables.Tenant.Name);
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                tenant = fileSystem.RemoveInvalidFileNameChars(tenant);
                root = Path.Combine(root, tenant);
            }

            return root;
        }

        // When a package has been installed once, Octopus gives users the ability to 'force' a redeployment of the package. 
        // This is often useful for example if a deployment partially completes and the installation is in an invalid state 
        // (e.g., corrupt files are left on disk, or the package is only half extracted). We *can't* just uninstall the package 
        // or overwrite the files, since they might be locked by IIS or another process. So instead we create a new unique 
        // directory. 
        static string EnsureTargetPathExistsAndIsEmpty(string desiredTargetPath, ICalamariFileSystem fileSystem)
        {
            var target = desiredTargetPath;

            using (Semaphore.Acquire("Octopus.Calamari.ExtractionDirectory", "Another process is finding an extraction directory, please wait..."))
            {
                for (var i = 1; fileSystem.DirectoryExists(target) || fileSystem.FileExists(target); i++)
                    target = desiredTargetPath + "_" + i;

                fileSystem.EnsureDirectoryExists(target);
            }

            return target;
        }
    }
}