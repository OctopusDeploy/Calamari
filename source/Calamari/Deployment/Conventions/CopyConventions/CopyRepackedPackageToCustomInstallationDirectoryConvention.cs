using System.IO;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions.CopyConventions
{
    /// <summary>
    /// This convention is used to copy a repacked packaged to a custom directory. For example, it is
    /// used when you want to deploy something like a Java WAR file to an application server deployment
    /// folder after the WAR has had the variable substituted.
    /// </summary>
    public class CopyRepackedPackageToCustomInstallationDirectoryConvention : BaseCopyConvention
    {
        public CopyRepackedPackageToCustomInstallationDirectoryConvention(ICalamariFileSystem fileSystem) :
            base(fileSystem)
        {
        }

        public override void Install(RunningDeployment deployment)
        {
            InstallTemplate(
                deployment,
                () => deployment.Variables.Get(SpecialVariables.RepackedArchiveLocation),
                (out string errorString) => deployment.Variables.Get(SpecialVariables.Package.CustomInstallationDirectory, out errorString),                
                sourceFile =>
                {
                    var dest = Path.Combine(deployment.CustomDirectory, Path.GetFileName(sourceFile));
                    
                    // Copy files from staging area to custom directory
                    Log.Info("Copying '{0}' to '{1}'", sourceFile, dest);
                    FileSystem.CopyFile(sourceFile, dest);
    
                    // From this point on, the current directory will be the custom-directory
                    deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;
    
                    Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath,
                        deployment.CustomDirectory, deployment.Variables);
                    Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath,
                        deployment.CustomDirectory, deployment.Variables);
                });
        }
    }
}