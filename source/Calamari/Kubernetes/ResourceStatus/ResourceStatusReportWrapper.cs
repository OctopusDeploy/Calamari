using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.FeatureToggles;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportWrapper : IScriptWrapper
    {
        private readonly ILog log;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusChecker resourceStatusChecker;

        public ResourceStatusReportWrapper(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IResourceStatusChecker resourceStatusChecker)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.resourceStatusChecker = resourceStatusChecker;
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

        public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var kubectl = new Kubectl(variables, log, commandLineRunner, workingDirectory, environmentVars);

            var resourceStatusReportExecutor =
                new ResourceStatusReportExecutor(variables, log, fileSystem, resourceStatusChecker, kubectl);


            var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
            if (result.ExitCode != 0)
            {
                return result;
            }

            try
            {
                resourceStatusReportExecutor.ReportStatus(workingDirectory);
            }
            catch (Exception e)
            {
                return new CommandResult("K8s Resource Status Reporting", 1, e.Message, workingDirectory);
            }

            return result;
        }
    }
}