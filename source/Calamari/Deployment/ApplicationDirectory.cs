using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes.Semaphores;
using Octopus.Versioning.Metadata;
using Octostache;

namespace Calamari.Deployment
{
    public class ApplicationDirectory
    {
        static readonly ISemaphoreFactory Semaphore = SemaphoreFactory.Get();

        /// <summary>
        /// Returns the directory where the package will be installed. 
        /// Also ensures the directory exists, and that there is free-space on the disk.
        /// </summary>
        public static string GetApplicationDirectory(PackageMetadata package, VariableDictionary variables, ICalamariFileSystem fileSystem)
        {
            return EnsureTargetPathExistsAndIsEmpty(
                Path.Combine(GetEnvironmentApplicationDirectory(fileSystem, variables), 
                package.PackageId, package.Version), fileSystem);
        }

        /// This will be specific to Tenant and/or Environment if these variables are available.
        static string GetEnvironmentApplicationDirectory(ICalamariFileSystem fileSystem, VariableDictionary variables)
        {
            var root = GetApplicationDirectoryRoot(fileSystem, variables);
            root = AppendTenantNameIfProvided(fileSystem, variables, root);
            root = AppendEnvironmentNameIfProvided(fileSystem, variables, root);

            fileSystem.EnsureDirectoryExists(root);
            fileSystem.EnsureDiskHasEnoughFreeSpace(root);

            return root;
        }

        static string GetApplicationDirectoryRoot(ICalamariFileSystem fileSystem, VariableDictionary variables)
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

        static string AppendEnvironmentNameIfProvided(ICalamariFileSystem fileSystem, VariableDictionary variables, string root)
        {
            var environment = variables.Get(SpecialVariables.Environment.Name);
            if (!string.IsNullOrWhiteSpace(environment))
            {
                environment = fileSystem.RemoveInvalidFileNameChars(environment);
                root = Path.Combine(root, environment);
            }

            return root;
        }

        static string AppendTenantNameIfProvided(ICalamariFileSystem fileSystem, VariableDictionary variables, string root)
        {
            var tenant = variables.Get(SpecialVariables.Deployment.Tenant.Name);
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
                {
                    target = desiredTargetPath + "_" + i;
                }

                fileSystem.EnsureDirectoryExists(target);
            }

            return target;
        }
    }
}