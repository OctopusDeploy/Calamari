using System.IO;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.FileSystem;
using FailureOptions = Calamari.Extensibility.FileSystem.FailureOptions;

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

            // Purge if requested
            if (deployment.Variables.GetFlag(
                SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment))
            {
                Log.Info("Purging the directory '{0}'", customInstallationDirectory);
                fileSystem.PurgeDirectory(deployment.CustomDirectory, FailureOptions.ThrowOnFailure);
            }

            // Copy files from staging area to custom directory
            Log.Info("Copying package contents to '{0}'", customInstallationDirectory);
            int count = fileSystem.CopyDirectory(deployment.StagingDirectory, deployment.CustomDirectory);
            Log.Info("Copied {0} files", count);

            // From this point on, the current directory will be the custom-directory
            deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;


            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, deployment.CustomDirectory, deployment.Variables);
            Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, deployment.CustomDirectory, deployment.Variables);
        }
    }
}