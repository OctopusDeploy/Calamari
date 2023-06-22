#if !NET40
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
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ResourceStatusReportExecutor statusReportExecutor;

        public ResourceStatusReportWrapper(IVariables variables,
            ICalamariFileSystem fileSystem,
            ResourceStatusReportExecutor statusReportExecutor)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.statusReportExecutor = statusReportExecutor;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesStatusCheckPriority;
        public IScriptWrapper NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            var resourceStatusEnabled = variables.GetFlag(SpecialVariables.ResourceStatusCheck);
            var isBlueGreen = variables.Get(SpecialVariables.DeploymentStyle) == "bluegreen";
            var isWaitDeployment = variables.Get(SpecialVariables.DeploymentWait) == "wait";
            if (!resourceStatusEnabled || isBlueGreen || isWaitDeployment)
            {
                return false;
            }

            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            return hasClusterUrl || hasClusterName;
        }

        public CommandResult ExecuteScript(
            Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

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
                var statusResult = statusReportExecutor.Start(resources).WaitForCompletionOrTimeout()
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
#endif