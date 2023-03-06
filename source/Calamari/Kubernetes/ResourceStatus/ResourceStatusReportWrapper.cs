using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.FeatureToggles;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportWrapper : IScriptWrapper
    {
        private readonly IVariables variables;
        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusChecker statusChecker;

        public ResourceStatusReportWrapper(IVariables variables, ILog log, ICalamariFileSystem fileSystem, IResourceStatusChecker statusChecker)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.statusChecker = statusChecker;
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
            var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);

            if (!TryReadManifestFile(out var content))
            {
                return result;
            }

            var customKubectlExecutable = variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable");

            var workingDirectory = Path.GetDirectoryName(script.File);
            
            var definedResources = KubernetesYaml.GetDefinedResources(content).ToList();

            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }
            
            var kubectl = new Kubectl(customKubectlExecutable, log, commandLineRunner, workingDirectory, environmentVars);
            if (!kubectl.TrySetKubectl())
            {
                return new CommandResult(string.Empty, 1);
            }

            statusChecker.CheckStatusUntilCompletion(definedResources, kubectl);

            return result;
        }

        private bool TryReadManifestFile(out string content)
        {
            // TODO this won't handle configMaps defined together with a Deploy a Container step
            var customResourceFileName =
                variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName");
            var knownFileNames = new[]
            {
                "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
            };
            foreach (var file in knownFileNames)
            {
                if (!fileSystem.FileExists(file))
                {
                    continue;
                }

                content = fileSystem.ReadFile(file);
                return true;
            }

            content = null;
            return false;
        }
    }
}