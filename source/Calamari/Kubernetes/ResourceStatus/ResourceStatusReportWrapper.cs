using System;
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
using Calamari.Kubernetes.ResourceStatus.Resources;

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
            var customKubectlExecutable = variables.Get(SpecialVariables.CustomKubectlExecutable);
            var deploymentTimeoutSeconds = variables.GetInt32(SpecialVariables.DeploymentTimeout) ?? 0;
            var stabilizationTimeoutSeconds = variables.GetInt32(SpecialVariables.StabilizationTimeout) ?? 0;
            var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
            var workingDirectory = Path.GetDirectoryName(script.File);
            
            var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
            if (result.ExitCode != 0)
            {
                return result;
            }

            var manifests = ReadManifestFiles().ToList();
            var definedResources = KubernetesYaml.GetDefinedResources(manifests, defaultNamespace).ToList();
            
            var secret = GetSecret(defaultNamespace);
            if (secret != null)
            {
                definedResources.Add(secret);
            }
            
            var configMap = GetConfigMap(defaultNamespace);
            if (configMap != null)
            {
                definedResources.Add(configMap);
            }

            if (!definedResources.Any())
            {
                return result;
            }

            var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");
            
            environmentVars.Add("KUBECONFIG", kubeConfig);
            
            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }
            
            var kubectl = new Kubectl(customKubectlExecutable, log, commandLineRunner, workingDirectory, environmentVars);
            if (!kubectl.TrySetKubectl())
            {
                return new CommandResult(string.Empty, 1);
            }

            var deploymentTimer = deploymentTimeoutSeconds == 0
                ? new InfiniteCountdownTimer() as ICountdownTimer
                : new CountdownTimer(TimeSpan.FromSeconds(deploymentTimeoutSeconds)) as ICountdownTimer;

            var stabilizationTimer = new CountdownTimer(TimeSpan.FromSeconds(stabilizationTimeoutSeconds));

            var stabilizingTimer = new StabilizingTimer(deploymentTimer, stabilizationTimer);
            
            var completedSuccessfully = statusChecker.CheckStatusUntilCompletionOrTimeout(definedResources, stabilizingTimer, kubectl);
            
            if (!completedSuccessfully)
            {
                return new CommandResult(string.Empty, 1, "Not all resources have deployed successfully within timeout");
            }
            
            return result;
        }

        private IEnumerable<string> ReadManifestFiles()
        {
            var customResourceFileName = variables.Get(SpecialVariables.CustomResourceYamlFileName);
            
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

                yield return fileSystem.ReadFile(file);
            }
        }
        
        private ResourceIdentifier GetConfigMap(string defaultNamespace)
        {
            if (!variables.GetFlag("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled"))
            {
                return null;
            }
            var configMapName = variables.Get("Octopus.Action.KubernetesContainers.ComputedConfigMapName");
            return string.IsNullOrEmpty(configMapName) ? null : new ResourceIdentifier("ConfigMap", configMapName, defaultNamespace);
        }
        
        private ResourceIdentifier GetSecret(string defaultNamespace)
        {
            if (!variables.GetFlag("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled"))
            {
                return null;
            }
            var secretName = variables.Get("Octopus.Action.KubernetesContainers.ComputedSecretName");
            return string.IsNullOrEmpty(secretName) ? null : new ResourceIdentifier("Secret", secretName, defaultNamespace);
        }
    }
}