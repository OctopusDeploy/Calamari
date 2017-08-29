using System;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class CopyPackageToCustomInstallationDirectoryConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public CopyPackageToCustomInstallationDirectoryConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            string errorString;
            var customInstallationDirectory = deployment.Variables.Get(SpecialVariables.Package.CustomInstallationDirectory, out errorString);
            var sourceDirectory = deployment.Variables.Get(SpecialVariables.OriginalPackageDirectoryPath);

            if (string.IsNullOrWhiteSpace(customInstallationDirectory))
            {
                Log.Verbose("The package has been installed to: " + sourceDirectory);
                Log.VerboseFormat(
                    "If you would like the package to be installed to an alternative location, please specify the variable '{0}'",
                    SpecialVariables.Package.CustomInstallationDirectory);
                // If the variable was not set then we set it, as it makes it simpler for anything to depend on it from this point on
                deployment.Variables.Set(SpecialVariables.Package.CustomInstallationDirectory,
                    sourceDirectory);

                return;
            }

            if (!string.IsNullOrEmpty(errorString))
            {
                throw new CommandException(
                    $"An error occurred when evaluating the value for the custom install directory. {errorString}");
            }

            if (string.IsNullOrEmpty(Path.GetPathRoot(customInstallationDirectory)))
            {
                throw new CommandException(
                    $"The custom install directory '{customInstallationDirectory}' is a relative path, please specify the path as an absolute path or a UNC path.");
            }

            if (customInstallationDirectory.IsChildOf(sourceDirectory))
            {
                throw new CommandException(
                    $"The custom install directory '{customInstallationDirectory}' is a child directory of the base installation directory '{sourceDirectory}', please specify a different destination.");
            }

            try
            {
                // Purge if requested
                if (deployment.Variables.GetFlag(
                    SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment))
                {
                    Log.Info("Purging the directory '{0}'", customInstallationDirectory);
                    var purgeExlusions = deployment.Variables.GetPaths(SpecialVariables.Package.CustomInstallationDirectoryPurgeExclusions).ToArray();
                    if (purgeExlusions.Any())
                    {
                        Log.Info("Leaving files and directories that match any of: '{0}'", string.Join(", ", purgeExlusions));
                    }
                    fileSystem.PurgeDirectory(deployment.CustomDirectory, FailureOptions.ThrowOnFailure, purgeExlusions);
                }

                // Copy files from staging area to custom directory
                Log.Info("Copying package contents to '{0}'", customInstallationDirectory);
                int count = fileSystem.CopyDirectory(deployment.StagingDirectory, deployment.CustomDirectory);
                Log.Info("Copied {0} files", count);

                // From this point on, the current directory will be the custom-directory
                deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;

                Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, deployment.CustomDirectory, deployment.Variables);
                Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, deployment.CustomDirectory, deployment.Variables);
                Log.SetOutputVariable(SpecialVariables.Package.Output.CopiedFileCount, count.ToString(), deployment.Variables);
            }
            catch (UnauthorizedAccessException uae) when (uae.Message.StartsWith("Access to the path"))
            {
                throw new CommandException(
                    $"{uae.Message} Ensure that the application that uses this directory is not running. " +
                    "If this is an IIS website, stop the application pool or use an app_offline.htm file " +
                    "(see https://g.octopushq.com/TakingWebsiteOffline)."
                );
            }
        }
    }
}