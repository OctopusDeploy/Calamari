using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.FeatureToggles;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportWrapper : IScriptWrapper
    {
        private readonly IVariables variables;
        private readonly ResourceStatusReportExecutor statusReportExecutor;

        public ResourceStatusReportWrapper(IVariables variables, ResourceStatusReportExecutor statusReportExecutor)
        {
            this.variables = variables;
            this.statusReportExecutor = statusReportExecutor;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesStatusCheckPriority;
        public IScriptWrapper NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            if (!FeatureToggle.KubernetesDeploymentStatusFeatureToggle.IsEnabled(variables))
            {
                return false;
            }

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

        public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
            if (result.ExitCode != 0)
            {
                return result;
            }

            var successful = statusReportExecutor.ReportStatus(workingDirectory, commandLineRunner, environmentVars);

            return successful ? result : new CommandResult("K8s Resource Status Reporting", 1);
        }
    }
}