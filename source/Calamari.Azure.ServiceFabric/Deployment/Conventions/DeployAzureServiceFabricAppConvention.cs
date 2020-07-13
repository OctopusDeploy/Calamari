using System;
using System.IO;
using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Util;

namespace Calamari.Azure.ServiceFabric.Deployment.Conventions
{
    public class DeployAzureServiceFabricAppConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureServiceFabricAppConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
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

        public void Install(RunningDeployment deployment)
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
                fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.DeployAzureServiceFabricApplication.ps1"));
            }

            var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }
        }

        void SetRegisterApplicationTypeTimeout(IVariables variables)
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
            DirectoryLoggingHelper.LogDirectoryContents(log, fileSystem, workingDirectory, "", 0);
        }
    }
}