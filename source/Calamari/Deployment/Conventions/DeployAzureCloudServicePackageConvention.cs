using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class DeployAzureCloudServicePackageConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources, 
            IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.Info("Config file: " + deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile));

            Log.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot), deployment.Variables);
            Log.SetOutputVariable("OctopusAzurePackageUri", deployment.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureDeploymentLabel", deployment.Variables.Get(SpecialVariables.Action.Name) + " v" + deployment.Variables.Get(SpecialVariables.Release.Number), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", deployment.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount), deployment.Variables);

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
               fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText("Calamari.Scripts.DeployAzureCloudService.ps1")); 
            }

            var result = scriptEngine.Execute(scriptFile, deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, DeletionOptions.TryThreeTimesIgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }
        }
    }
}