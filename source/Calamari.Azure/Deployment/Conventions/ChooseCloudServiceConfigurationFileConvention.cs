using System.IO;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ChooseCloudServiceConfigurationFileConvention : IConvention
    {
        static readonly string FallbackFileName = "ServiceConfiguration.Cloud.cscfg"; // Default from Visual Studio
        static readonly string EnvironmentFallbackFileName = "ServiceConfiguration.{0}.cscfg";
        readonly ICalamariFileSystem fileSystem;
        private readonly ILog log = Log.Instance;

        public ChooseCloudServiceConfigurationFileConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Run(IExecutionContext deployment)
        {
            var configurationFile = ChooseWhichConfigurationFileToUse(deployment);
            log.SetOutputVariable(SpecialVariables.Action.Azure.Output.ConfigurationFile,
                configurationFile, deployment.Variables);
        }

        string ChooseWhichConfigurationFileToUse(IExecutionContext deployment)
        {
            var configurationFilePath = GetFirstExistingFile(deployment,
                deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceConfigurationFileRelativePath),
                BuildEnvironmentSpecificFallbackFileName(deployment),
                FallbackFileName);

            return configurationFilePath;
        }

        string GetFirstExistingFile(IExecutionContext deployment, params string[] fileNames)
        {
            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var path = Path.Combine(deployment.CurrentDirectory, name);
                if (fileSystem.FileExists(path))
                {
                    log.Verbose("Found Azure Cloud Service Configuration file: " + path);
                    return path;
                }

                log.Verbose("Azure Cloud Service Configuration file (*.cscfg) not found: " + path);
            }

            throw new CommandException(
                "Could not find an Azure Cloud Service Configuration file (*.cscfg) in the package.");
        }

        static string BuildEnvironmentSpecificFallbackFileName(IExecutionContext deployment)
        {
            return string.Format(EnvironmentFallbackFileName,
                deployment.Variables.Get(SpecialVariables.Environment.Name));
        }
    }
}