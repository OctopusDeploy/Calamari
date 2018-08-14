using System.IO;
using System.Reflection;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Azure.Deployment.Conventions
{
    public class SwapAzureDeploymentConvention : IConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptRunner scriptEngine;
        private readonly ILog log = Log.Instance;

        public SwapAzureDeploymentConvention(ICalamariFileSystem fileSystem,
            ICalamariEmbeddedResources embeddedResources,
            IScriptRunner scriptEngine)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
        }

        public void Run(IExecutionContext deployment)
        {
            log.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot), deployment.Variables);
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deployment.Variables.Get(SpecialVariables.Action.Name) + " v" + deployment.Variables.Get(SpecialVariables.Release.Number), deployment.Variables);
            log.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), deployment.Variables);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            var scriptFile = Path.Combine(tempDirectory, "SwapAzureCloudServiceDeployment.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.SwapAzureCloudServiceDeployment.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile));

            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException($"Script '{scriptFile}' returned non-zero exit code: {result.ExitCode}");
            }

            var swapped = deployment.Variables.GetFlag(SpecialVariables.Action.Azure.Output.CloudServiceDeploymentSwapped);
            if (swapped)
            {
                deployment.Variables.Set(SpecialVariables.Action.SkipRemainingConventions, "true");
            }
        }
    }
}
