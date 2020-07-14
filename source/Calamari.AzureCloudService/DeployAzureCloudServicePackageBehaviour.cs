using System.IO;
using System.Threading.Tasks;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
using Calamari.CommonTemp;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using KnownVariables = Calamari.CommonTemp.KnownVariables;

namespace Calamari.AzureCloudService
{
    public class DeployAzureCloudServicePackageBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureCloudServicePackageBehaviour(ILog log, ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources,
            IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public Task Execute(RunningDeployment deployment)
        {
            log.Info("Config file: " + deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile));

            log.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot), deployment.Variables);
            log.SetOutputVariable("OctopusAzurePackageUri", deployment.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri), deployment.Variables);

            var deploymentLabel = deployment.Variables.Get(SpecialVariables.Action.Azure.DeploymentLabel, defaultValue: deployment.Variables.Get(ActionVariables.Name) + " v" + deployment.Variables.Get(KnownVariables.Release.Number));
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deploymentLabel, deployment.Variables);

            log.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), deployment.Variables);
            log.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", deployment.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount), deployment.Variables);

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.DeployAzureCloudService.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }

            return this.CompletedTask();
        }
    }
}