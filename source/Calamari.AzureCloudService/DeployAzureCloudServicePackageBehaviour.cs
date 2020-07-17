using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.CommonTemp;
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

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            log.Info("Config file: " + context.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile));

            log.SetOutputVariable("OctopusAzureServiceName", context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), context.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), context.Variables);
            log.SetOutputVariable("OctopusAzureSlot", context.Variables.Get(SpecialVariables.Action.Azure.Slot), context.Variables);
            log.SetOutputVariable("OctopusAzurePackageUri", context.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri), context.Variables);

            var deploymentLabel = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentLabel, defaultValue: context.Variables.Get(ActionVariables.Name) + " v" + context.Variables.Get(KnownVariables.Release.Number));
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deploymentLabel, context.Variables);

            log.SetOutputVariable("OctopusAzureSwapIfPossible", context.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), context.Variables);
            log.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", context.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount), context.Variables);

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(context.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.DeployAzureCloudService.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile), context.Variables, commandLineRunner);

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