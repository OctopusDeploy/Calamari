using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportExecutor
    {
        private readonly IVariables variables;
        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusChecker statusChecker;

        public ResourceStatusReportExecutor(IVariables variables, ILog log, ICalamariFileSystem fileSystem, IResourceStatusChecker statusChecker)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.statusChecker = statusChecker;
        }

        public void ReportStatus(string workingDirectory, ICommandLineRunner commandLineRunner, Dictionary<string, string> environmentVars = null, Kubectl kubectl = null)
        {
            var customKubectlExecutable = variables.Get(SpecialVariables.CustomKubectlExecutable);
            var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
            // When the namespace on a target was set and then cleared, it's going to be "" instead of null
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                defaultNamespace = "default";
            }
            var deploymentTimeoutSeconds = variables.GetInt32(SpecialVariables.DeploymentTimeout) ?? 0;
            var stabilizationTimeoutSeconds = variables.GetInt32(SpecialVariables.StabilizationTimeout) ?? 0;

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
                log.Verbose("No defined resources are found, skipping resource status check");
                return;
            }

            log.Verbose("Performing resource status checks on the following resources:");
            foreach (var resourceIdentifier in definedResources)
            {
                log.Verbose($" - {resourceIdentifier.Kind}/{resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");
            }

            var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

            if (kubectl is null)
            {
                if (environmentVars == null)
                {
                    environmentVars = new Dictionary<string, string>();
                }
                environmentVars["KUBECONFIG"] = kubeConfig;

                foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
                {
                    environmentVars[proxyVariable.Key] = proxyVariable.Value;
                }

                kubectl = new Kubectl(customKubectlExecutable, log, commandLineRunner, workingDirectory, environmentVars);
            }

            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Unable to set KubeCtl");
            }

            var deploymentTimer = deploymentTimeoutSeconds == 0
                ? new InfiniteCountdownTimer() as ICountdownTimer
                : new CountdownTimer(TimeSpan.FromSeconds(deploymentTimeoutSeconds));

            var stabilizationTimer = new CountdownTimer(TimeSpan.FromSeconds(stabilizationTimeoutSeconds));

            var stabilizingTimer = new StabilizingTimer(deploymentTimer, stabilizationTimer, log);

            var completedSuccessfully = statusChecker.CheckStatusUntilCompletionOrTimeout(definedResources, stabilizingTimer, kubectl);

            if (!completedSuccessfully)
            {
                throw new TimeoutException("Not all resources have deployed successfully within timeout");
            }
        }

        private IEnumerable<string> ReadManifestFiles()
        {
            return from file in GetManifestFileNames() where fileSystem.FileExists(file) select fileSystem.ReadFile(file);
        }

        private IEnumerable<string> GetManifestFileNames()
        {
            var groupedDirectories = variables.Get(SpecialVariables.GroupedYamlDirectories);
            if (groupedDirectories != null)
            {
                return groupedDirectories.Split(';').SelectMany(d => fileSystem.EnumerateFiles(d));
            }

            var customResourceFileName =
                variables.Get(SpecialVariables.CustomResourceYamlFileName);

            return new[]
            {
                "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
            };
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