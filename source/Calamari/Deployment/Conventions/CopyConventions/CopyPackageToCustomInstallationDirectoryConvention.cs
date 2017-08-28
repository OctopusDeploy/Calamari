using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions.CopyConventions
{
    public class CopyPackageToCustomInstallationDirectoryConvention : BaseCopyConvention
    {
        public CopyPackageToCustomInstallationDirectoryConvention(ICalamariFileSystem fileSystem) :
            base(fileSystem)
        {

        }

        public override void Install(RunningDeployment deployment)
        {
            InstallTemplate(
                deployment,                
                () => deployment.Variables.Get(SpecialVariables.OriginalPackageDirectoryPath),
                (out string errorString) => deployment.Variables.Get(SpecialVariables.Package.CustomInstallationDirectory, out errorString),
                sourceDirectory =>
                {
                    // Copy files from staging area to custom directory
                    Log.Info("Copying package contents to '{0}'", deployment.CustomDirectory);
                    int count = FileSystem.CopyDirectory(deployment.StagingDirectory, deployment.CustomDirectory);
                    Log.Info("Copied {0} files", count);
    
                    // From this point on, the current directory will be the custom-directory
                    deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;
    
                    Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath,
                        deployment.CustomDirectory, deployment.Variables);
                    Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath,
                        deployment.CustomDirectory, deployment.Variables);
                    Log.SetOutputVariable(SpecialVariables.Package.Output.CopiedFileCount, count.ToString(),
                        deployment.Variables);
                });
            }
    }
}