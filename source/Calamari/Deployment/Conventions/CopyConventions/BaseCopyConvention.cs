using System;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions.CopyConventions
{
    /// <summary>
    /// Deletegate for a method that returns a string and an error message
    /// </summary>
    public delegate string OutAction(out string errorMessage);
    
    /// <summary>
    /// This class provides a template for copy operations. It is expected to be extended by
    /// convents that copy extracted or repacked packages.
    /// </summary>
    public abstract class BaseCopyConvention : IInstallConvention
    {
        protected readonly ICalamariFileSystem FileSystem;

        protected BaseCopyConvention(ICalamariFileSystem fileSystem)
        {
            this.FileSystem = fileSystem;
        }
        
        protected void InstallTemplate(
            RunningDeployment deployment, 
            Func<string> getSourceDir, 
            OutAction getDestDir, 
            Action<string> doCopy)
        {
            string errorMessage;
            var sourceDirectory = getSourceDir();
            var customInstallationDirectory = getDestDir(out errorMessage);
            
            if (!CheckTargetDir(deployment, customInstallationDirectory, sourceDirectory))
            {
                return;
            }           

            ValidateInput(errorMessage, customInstallationDirectory, sourceDirectory);
           
            try
            {
                PurgeTargetDir(deployment, customInstallationDirectory);
                doCopy(sourceDirectory);
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

        /// <summary>
        /// Checks to see if the user supplied a target directory
        /// </summary>
        /// <returns>true to continue processing, and false to exit early</returns>
        protected bool CheckTargetDir(RunningDeployment deployment, string customInstallationDirectory, string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(customInstallationDirectory))
            {
                Log.Verbose("The package has been installed to: " + sourceDirectory);
                Log.VerboseFormat(
                    "If you would like the package to be installed to an alternative location, please specify the variable '{0}'",
                    SpecialVariables.Package.CustomInstallationDirectory);
                // If the variable was not set then we set it, as it makes it simpler for anything to depend on it from this point on
                deployment.Variables.Set(
                    SpecialVariables.Package.CustomInstallationDirectory,
                    sourceDirectory);

                return false;
            }

            return true;
        }

        protected void ValidateInput(string errorString, string customInstallationDirectory, string sourceDirectory)
        {
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
        }

        protected void PurgeTargetDir(RunningDeployment deployment, string customInstallationDirectory)
        {
            if (deployment.Variables.GetFlag(
                SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment))
            {
                Log.Info("Purging the directory '{0}'", customInstallationDirectory);
                var purgeExlusions = deployment.Variables.GetPaths(SpecialVariables.Package.CustomInstallationDirectoryPurgeExclusions).ToArray();
                if (purgeExlusions.Any())
                {
                    Log.Info("Leaving files and directories that match any of: '{0}'", string.Join(", ", purgeExlusions));
                }
                FileSystem.PurgeDirectory(deployment.CustomDirectory, FailureOptions.ThrowOnFailure, purgeExlusions);
            }
        }

        public abstract void Install(RunningDeployment deployment);
    }     
}