using System;
using System.Reflection;
using System.IO;
using Calamari.Azure.Util;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;
using Calamari.Util;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureServiceFabricAppConvention : IConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptRunner scriptEngine;
        private readonly ILog log = Log.Instance;

        public DeployAzureServiceFabricAppConvention(ICalamariFileSystem fileSystem,
            ICalamariEmbeddedResources embeddedResources,
            IScriptRunner scriptEngine)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
        }

        public void Run(IExecutionContext deployment)
        {
            var variables = deployment.Variables;

            // Set output variables for our script to access.
            log.SetOutputVariable("PublishProfileFile", variables.Get(SpecialVariables.Action.ServiceFabric.PublishProfileFile, "PublishProfiles\\Cloud.xml"), variables);
            log.SetOutputVariable("DeployOnly", variables.Get(SpecialVariables.Action.ServiceFabric.DeployOnly, defaultValue: false.ToString()), variables);
            log.SetOutputVariable("UnregisterUnusedApplicationVersionsAfterUpgrade", variables.Get(SpecialVariables.Action.ServiceFabric.UnregisterUnusedApplicationVersionsAfterUpgrade, defaultValue: false.ToString()), variables);
            log.SetOutputVariable("OverrideUpgradeBehavior", variables.Get(SpecialVariables.Action.ServiceFabric.OverrideUpgradeBehavior, defaultValue: "None"), variables);
            log.SetOutputVariable("OverwriteBehavior", variables.Get(SpecialVariables.Action.ServiceFabric.OverwriteBehavior, defaultValue: "SameAppTypeAndVersion"), variables);
            log.SetOutputVariable("SkipPackageValidation", variables.Get(SpecialVariables.Action.ServiceFabric.SkipPackageValidation, defaultValue: false.ToString()), variables);
            log.SetOutputVariable("CopyPackageTimeoutSec", variables.Get(SpecialVariables.Action.ServiceFabric.CopyPackageTimeoutSec, defaultValue: 0.ToString()), variables);
            SetRegisterApplicationTypeTimeout(variables);


            // Package should have been extracted to the staging dir (as per the ExtractPackageToStagingDirectoryConvention).
            var targetPath = Path.Combine(Environment.CurrentDirectory, "staging");
            log.SetOutputVariable("ApplicationPackagePath", targetPath, variables);

            if (deployment.Variables.GetFlag(SpecialVariables.Action.ServiceFabric.LogExtractedApplicationPackage))
                LogExtractedPackage(deployment.CurrentDirectory);

            // The user may supply the script, to override behaviour.
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToServiceFabric.ps1");
            if (!fileSystem.FileExists(scriptFile))
            {
                // Use our bundled version.
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.DeployAzureServiceFabricApplication.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile));

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }
        }

        void SetRegisterApplicationTypeTimeout(VariableDictionary variables)
        {
            var registerAppTypeTimeout = variables.Get(SpecialVariables.Action.ServiceFabric.RegisterApplicationTypeTimeoutSec);
            if (registerAppTypeTimeout != null)
            {
                log.SetOutputVariable("RegisterApplicationTypeTimeoutSec", registerAppTypeTimeout, variables);
            }
        }

        void LogExtractedPackage(string workingDirectory)
        {
            log.Verbose("Service Fabric extracted. Working directory contents:");
            DirectoryLoggingHelper.LogDirectoryContents(fileSystem, log, workingDirectory, "", 0);
        }
    }
}