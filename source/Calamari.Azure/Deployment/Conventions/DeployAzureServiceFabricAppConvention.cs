using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.IO;
using Calamari.Util;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureServiceFabricAppConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureServiceFabricAppConvention(ICalamariFileSystem fileSystem,
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
            Log.Info("Config file: " + deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile));

            var variables = deployment.Variables;

            var test = variables.Get(SpecialVariables.Action.Azure.FabricPublishProfileFile);

            // Vars we know will exist from our step/action.
            ///Log.SetOutputVariable("ConnectionEndpoint", variables.Get(SpecialVariables.Action.Azure.FabricConnectionEndpoint), variables);
            Log.SetOutputVariable("PublishProfileFile", variables.Get(SpecialVariables.Action.Azure.FabricPublishProfileFile), variables);

            // Package should have been extracted to the staging dir (as per the ExtractPackageToStagingDirectoryConvention).
            var targetPath = Path.Combine(CrossPlatform.GetCurrentDirectory(), "staging");
            Log.SetOutputVariable("ApplicationPackagePath", targetPath, variables);
            
            Log.SetOutputVariable("DeployOnly", variables.Get(SpecialVariables.Action.Azure.FabricDeployOnly, defaultValue: false.ToString()), variables);
            ///Log.SetOutputVariable("ApplicationParameters", variables.Get(SpecialVariables.Action.Azure.FabricApplicationParameters, defaultValue: string.Empty), variables);
            Log.SetOutputVariable("UnregisterUnusedApplicationVersionsAfterUpgrade", variables.Get(SpecialVariables.Action.Azure.FabricUnregisterUnusedApplicationVersionsAfterUpgrade, defaultValue: false.ToString()), variables);
            Log.SetOutputVariable("OverrideUpgradeBehavior", variables.Get(SpecialVariables.Action.Azure.FabricOverrideUpgradeBehavior, defaultValue: "None"), variables);
            Log.SetOutputVariable("UseExistingClusterConnection", variables.Get(SpecialVariables.Action.Azure.FabricUseExistingClusterConnection, defaultValue: false.ToString()), variables);
            Log.SetOutputVariable("OverwriteBehavior", variables.Get(SpecialVariables.Action.Azure.FabricOverwriteBehavior, defaultValue: "SameAppTypeAndVersion"), variables);
            Log.SetOutputVariable("SkipPackageValidation", variables.Get(SpecialVariables.Action.Azure.FabricSkipPackageValidation, defaultValue: false.ToString()), variables);
            Log.SetOutputVariable("SecurityToken", variables.Get(SpecialVariables.Action.Azure.FabricSecurityToken, defaultValue: string.Empty), variables);
            Log.SetOutputVariable("CopyPackageTimeoutSec", variables.Get(SpecialVariables.Action.Azure.FabricCopyPackageTimeoutSec, defaultValue: 0.ToString()), variables);

            // The script name 'DeployToAzure.ps1' is used for consistency with other Octopus Azure steps.
            // The user may supply the script, to override behaviour.
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToAzure.ps1");
            // TODO: markse - uncomment these to always run our script during debug.
            //if (!fileSystem.FileExists(scriptFile))
            //{
                // Use our bundled version.
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.DeployAzureFabricApplication.ps1"));
            //}

            var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }

            /*
            EXAMPLE
            >. 'C:\Development\_archives\MarktopusSFA\MarktopusSFA\Scripts\Deploy-FabricApplication.ps1' 
            -ApplicationPackagePath 'C:\Development\_archives\MarktopusSFA\MarktopusSFA\pkg\Debug' 
            -PublishProfileFile 'C:\Development\_archives\MarktopusSFA\MarktopusSFA\PublishProfiles\Cloud.xml' 
            -DeployOnly:$false 
            -ApplicationParameter:@{} 
            -UnregisterUnusedApplicationVersionsAfterUpgrade $false 
            -OverrideUpgradeBehavior 'None' 
            -OverwriteBehavior 'SameAppTypeAndVersion' 
            -SkipPackageValidation:$false 
            -ErrorAction Stop
            */
        }
    }
}