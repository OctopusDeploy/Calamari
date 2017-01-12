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

            Log.SetOutputVariable("OctopusServiceFabricConnectionEndpoint", deployment.Variables.Get(SpecialVariables.Action.Azure.ServiceFabricConnectionEndpoint), deployment.Variables);
            Log.SetOutputVariable("OctopusServiceFabricTargetProfile", deployment.Variables.Get(SpecialVariables.Action.Azure.ServiceFabricTargetProfile), deployment.Variables);
            
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