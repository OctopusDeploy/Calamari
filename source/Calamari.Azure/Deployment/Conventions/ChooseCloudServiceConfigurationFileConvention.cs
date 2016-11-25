using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensibility;
using Calamari.Integration.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ChooseCloudServiceConfigurationFileConvention : IInstallConvention
    {
        static readonly string FallbackFileName = "ServiceConfiguration.Cloud.cscfg"; // Default from Visual Studio
        static readonly string EnvironmentFallbackFileName = "ServiceConfiguration.{0}.cscfg";
        readonly ICalamariFileSystem fileSystem;

        public ChooseCloudServiceConfigurationFileConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var configurationFile = ChooseWhichConfigurationFileToUse(deployment);
            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.ConfigurationFile,
                configurationFile, deployment.Variables);
        }

        string ChooseWhichConfigurationFileToUse(RunningDeployment deployment)
        {
            var configurationFilePath = GetFirstExistingFile(deployment,
                deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceConfigurationFileRelativePath),
                BuildEnvironmentSpecificFallbackFileName(deployment),
                FallbackFileName);

            return configurationFilePath;
        }

        string GetFirstExistingFile(RunningDeployment deployment, params string[] fileNames)
        {
            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var path = Path.Combine(deployment.CurrentDirectory, name);
                if (fileSystem.FileExists(path))
                {
                    Log.Verbose("Found Azure Cloud Service Configuration file: " + path);
                    return path;
                }

                Log.Verbose("Azure Cloud Service Configuration file (*.cscfg) not found: " + path);
            }

            throw new CommandException(
                "Could not find an Azure Cloud Service Configuration file (*.cscfg) in the package.");
        }

        static string BuildEnvironmentSpecificFallbackFileName(RunningDeployment deployment)
        {
            return string.Format(EnvironmentFallbackFileName,
                deployment.Variables.Get(SpecialVariables.Environment.Name));
        }
    }
}