using System;
using System.IO;
using System.Reflection;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureCloudServicePackageConvention : IConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptRunner scriptEngine;
        private readonly ILog log = Log.Instance;

        public DeployAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources, 
            IScriptRunner scriptEngine)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.log = log;
        }

        public void Run(IExecutionContext deployment)
        {
            log.Info("Config file: " + deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile));

            log.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), deployment.Variables);
            log.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot), deployment.Variables);
            log.SetOutputVariable("OctopusAzurePackageUri", deployment.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri), deployment.Variables);

            var deploymentLabel = deployment.Variables.Get(SpecialVariables.Action.Azure.DeploymentLabel, defaultValue: deployment.Variables.Get(SpecialVariables.Action.Name) + " v" + deployment.Variables.Get(SpecialVariables.Release.Number));
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deploymentLabel, deployment.Variables);

            log.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()), deployment.Variables);
            log.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", deployment.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount), deployment.Variables);

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
               fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.DeployAzureCloudService.ps1")); 
            }

            var result = scriptEngine.Execute(new Script(scriptFile));

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile, result.ExitCode));
            }
        }
    }
}