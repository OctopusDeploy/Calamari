using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportWrapper : IScriptWrapper
    {
        private readonly Kubectl kubectl;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusReportExecutor statusReportExecutor;

        public ResourceStatusReportWrapper(
            Kubectl kubectl,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IResourceStatusReportExecutor statusReportExecutor)
        {
            this.kubectl = kubectl;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.statusReportExecutor = statusReportExecutor;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesStatusCheckPriority;
        public IScriptWrapper NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            var isBlueGreen = string.Equals(variables.Get(SpecialVariables.DeploymentStyle), "bluegreen", StringComparison.OrdinalIgnoreCase);
            var isWaitDeployment = string.Equals(variables.Get(SpecialVariables.DeploymentWait) , "wait", StringComparison.OrdinalIgnoreCase);

            //A blue/green or deployment wait is waiting for other things, so we don't run resource status check 
            if (isBlueGreen || isWaitDeployment)
            {
                return false;
            }

            // At this point, we only care about the status of the resource status check
            // If someone has set this variable manually then it might blow up, but that's not a supported configuration
            return variables.GetFlag(SpecialVariables.ResourceStatusCheck);
        }

        public CommandResult ExecuteScript(
            Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            kubectl.SetWorkingDirectory(workingDirectory);
            kubectl.SetEnvironmentVariables(environmentVars);
            var resourceFinder = new ResourceFinder(variables, fileSystem);

            var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
            if (result.ExitCode != 0)
            {
                return result;
            }

            CommandResult GetStatusResult(string errorMessage) =>
                new CommandResult("K8s Resource Status Reporting", 1, errorMessage, workingDirectory);

            try
            {
                var resources = resourceFinder.FindResources(workingDirectory);
                var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
                var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);
                var statusResult = statusReportExecutor.Start(timeoutSeconds, waitForJobs, resources).WaitForCompletionOrTimeout()
                                                       .GetAwaiter().GetResult();
                if (!statusResult)
                {
                    return GetStatusResult("Unable to complete Report Status, see log for details.");
                }
            }
            catch (Exception e)
            {
                return GetStatusResult(e.Message);
            }

            return result;
        }
    }
}