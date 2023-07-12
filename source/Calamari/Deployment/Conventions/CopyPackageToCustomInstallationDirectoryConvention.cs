using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

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
            var variables = deployment.Variables;
            string errorString;
            var customInstallationDirectory = variables.Get(PackageVariables.CustomInstallationDirectory, out errorString);
            var sourceDirectory = variables.Get(KnownVariables.OriginalPackageDirectoryPath);

            if (string.IsNullOrWhiteSpace(customInstallationDirectory))
            {
                Log.Verbose("The package has been installed to: " + sourceDirectory);
                Log.Verbose("If you would like the package to be installed to an alternative location, please use the 'Custom installation directory' feature");
                // If the variable was not set then we set it, as it makes it simpler for anything to depend on it from this point on
                variables.Set(PackageVariables.CustomInstallationDirectory,
                    sourceDirectory);

                return;
            }

            Log.Verbose($"Installing package to custom directory {customInstallationDirectory}");

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
                if (variables.GetFlag(
                    PackageVariables.CustomInstallationDirectoryShouldBePurgedBeforeDeployment))
                {
                    Log.Info("Purging the directory '{0}'", customInstallationDirectory);
                    var purgeExlusions = variables.GetPaths(PackageVariables.CustomInstallationDirectoryPurgeExclusions).ToArray();
                    if (purgeExlusions.Any())
                    {
                        Log.Info("Leaving files and directories that match any of: '{0}'", string.Join(", ", purgeExlusions));
                    }

                    var globMode = GlobModeRetriever.GetFromVariables(variables);
                    fileSystem.PurgeDirectory(deployment.CustomDirectory, FailureOptions.ThrowOnFailure, globMode, purgeExlusions);
                }

                // Copy files from staging area to custom directory
                Log.Info("Copying package contents to '{0}'", customInstallationDirectory);
                int count = fileSystem.CopyDirectory(deployment.StagingDirectory, deployment.CustomDirectory);
                Log.Info("Copied {0} files", count);

                // From this point on, the current directory will be the custom-directory
                deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;

                Log.SetOutputVariable(PackageVariables.Output.InstallationDirectoryPath, deployment.CustomDirectory, variables);
                Log.SetOutputVariable(PackageVariables.Output.DeprecatedInstallationDirectoryPath, deployment.CustomDirectory, variables);
                Log.SetOutputVariable(PackageVariables.Output.CopiedFileCount, count.ToString(), variables);
            }
            catch (UnauthorizedAccessException uae) when (uae.Message.StartsWith("Access to the path"))
            {
                var message = $"{uae.Message} Ensure that the application that uses this directory is not running.";
                if (CalamariEnvironment.IsRunningOnWindows)
                {
                    message += " If this is an IIS website, stop the application pool or use an app_offline.htm file " +
                               "(see https://g.octopushq.com/TakingWebsiteOffline).";
                }
                throw new CommandException(
                    message
                );
            }
        }
    }
}