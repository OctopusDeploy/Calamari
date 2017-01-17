using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureServiceFabricAppConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureServiceFabricAppConvention(ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources,
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

            var variables = deployment.Variables;
            Log.SetOutputVariable("OctopusAzureFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.Azure.FabricConnectionEndpoint), variables);
            Log.SetOutputVariable("OctopusAzureFabricPublishProfileFile", variables.Get(SpecialVariables.Action.Azure.FabricPublishProfileFile), variables);
            //Log.SetOutputVariable("OctopusAzureFabricApplicationPackagePath", variables.Get(SpecialVariables.Action.Azure.FabricApplicationPackagePath), variables);
            Log.SetOutputVariable("OctopusAzureFabricDeployOnly", variables.Get(SpecialVariables.Action.Azure.FabricDeployOnly), variables);
            Log.SetOutputVariable("OctopusAzureFabricApplicationParameters", variables.Get(SpecialVariables.Action.Azure.FabricApplicationParameters), variables);
            Log.SetOutputVariable("OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade", variables.Get(SpecialVariables.Action.Azure.FabricUnregisterUnusedApplicationVersionsAfterUpgrade), variables);
            Log.SetOutputVariable("OctopusAzureFabricOverrideUpgradeBehavior", variables.Get(SpecialVariables.Action.Azure.FabricOverrideUpgradeBehavior), variables);
            Log.SetOutputVariable("OctopusAzureFabricUseExistingClusterConnection", variables.Get(SpecialVariables.Action.Azure.FabricUseExistingClusterConnection), variables);
            Log.SetOutputVariable("OctopusAzureFabricOverwriteBehavior", variables.Get(SpecialVariables.Action.Azure.FabricOverwriteBehavior), variables);
            Log.SetOutputVariable("OctopusAzureFabricSkipPackageValidation", variables.Get(SpecialVariables.Action.Azure.FabricSkipPackageValidation), variables);
            //Log.SetOutputVariable("OctopusAzureFabricSecurityToken", variables.Get(SpecialVariables.Action.Azure.FabricSecurityToken), variables);
            Log.SetOutputVariable("OctopusAzureFabricCopyPackageTimeoutSec", variables.Get(SpecialVariables.Action.Azure.FabricCopyPackageTimeoutSec), variables);
            
            var scriptFile = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.DeployAzureServiceFabricApp.ps1");
            
            var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }
        }
    }
}