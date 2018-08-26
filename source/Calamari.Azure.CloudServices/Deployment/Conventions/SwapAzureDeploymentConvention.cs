using System.IO;
using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Azure.CloudServices.Deployment.Conventions
{
    public class SwapAzureDeploymentConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public SwapAzureDeploymentConvention(ICalamariFileSystem fileSystem,
            ICalamariEmbeddedResources embeddedResources,
            IScriptEngine scriptEngine,
            ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureDeploymentLabel", deployment.Variables.Get(SpecialVariables.Action.Name) + " v" + deployment.Variables.Get(SpecialVariables.Release.Number), deployment.Variables);
            Log.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), deployment.Variables);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            var scriptFile = Path.Combine(tempDirectory, "SwapAzureCloudServiceDeployment.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.SwapAzureCloudServiceDeployment.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

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
