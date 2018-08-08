using System.IO;
using Calamari.Azure.Commands;
using Calamari.Shared;
using Calamari.Shared.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ChooseCloudServiceConfigurationFileConvention : IConvention
    {
        static readonly string FallbackFileName = "ServiceConfiguration.Cloud.cscfg"; // Default from Visual Studio
        static readonly string EnvironmentFallbackFileName = "ServiceConfiguration.{0}.cscfg";
        readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;

        public ChooseCloudServiceConfigurationFileConvention(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void Run(IExecutionContext context)
        {
            var configurationFile = ChooseWhichConfigurationFileToUse(context);
            log.SetOutputVariable(SpecialVariables.Action.Azure.Output.ConfigurationFile,
                configurationFile, context.Variables);
        }

        string ChooseWhichConfigurationFileToUse(IExecutionContext context)
        {
            var configurationFilePath = GetFirstExistingFile(context,
                context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceConfigurationFileRelativePath),
                BuildEnvironmentSpecificFallbackFileName(context),
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

        static string BuildEnvironmentSpecificFallbackFileName(IExecutionContext context)
        {
            return string.Format(EnvironmentFallbackFileName,
                context.Variables.Get(SpecialVariables.Environment.Name));
        }
    }
}