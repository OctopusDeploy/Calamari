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
    public class SwapAzureDeploymentBehaviour : IBeforePackageExtractionBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public SwapAzureDeploymentBehaviour(ILog log, ICalamariFileSystem fileSystem,
            ICalamariEmbeddedResources embeddedResources,
            IScriptEngine scriptEngine,
            ICommandLineRunner commandLineRunner)
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
            log.SetOutputVariable("OctopusAzureServiceName", context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), context.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), context.Variables);
            log.SetOutputVariable("OctopusAzureSlot", context.Variables.Get(SpecialVariables.Action.Azure.Slot), context.Variables);
            log.SetOutputVariable("OctopusAzureDeploymentLabel", context.Variables.Get(ActionVariables.Name) + " v" + context.Variables.Get(KnownVariables.Release.Number), context.Variables);
            log.SetOutputVariable("OctopusAzureSwapIfPossible", context.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), context.Variables);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            var scriptFile = Path.Combine(tempDirectory, "SwapAzureCloudServiceDeployment.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.SwapAzureCloudServiceDeployment.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile), context.Variables, commandLineRunner);

            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException($"Script '{scriptFile}' returned non-zero exit code: {result.ExitCode}");
            }

            var swapped = context.Variables.GetFlag(SpecialVariables.Action.Azure.Output.CloudServiceDeploymentSwapped);
            if (swapped)
            {
                context.Variables.Set(KnownVariables.Action.SkipRemainingConventions, "true");
            }

            return this.CompletedTask();
        }
    }
}