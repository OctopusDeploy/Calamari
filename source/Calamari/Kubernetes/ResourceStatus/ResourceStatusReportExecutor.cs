using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportExecutor
    {
        private const int PollingIntervalSeconds = 2;

        private readonly IVariables variables;
        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusChecker statusChecker;
        private readonly Kubectl kubectl;

        public ResourceStatusReportExecutor(
            IVariables variables,
            ILog log,
            ICalamariFileSystem fileSystem,
            IResourceStatusChecker statusChecker,
            Kubectl kubectl)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.statusChecker = statusChecker;
            this.kubectl = kubectl;
        }

        public void ReportStatus(string workingDirectory)
        {
            var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
            // When the namespace on a target was set and then cleared, it's going to be "" instead of null
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                defaultNamespace = "default";
            }
            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var manifests = ReadManifestFiles(workingDirectory).ToList();
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

            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Unable to set KubeCtl");
            }

            var timer = timeoutSeconds == 0
                ? new InfiniteTimer(TimeSpan.FromSeconds(PollingIntervalSeconds)) as ITimer
                : new Timer(TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromSeconds(PollingIntervalSeconds));

            var completedSuccessfully = statusChecker.CheckStatusUntilCompletionOrTimeout(definedResources, timer, kubectl, new Options() {  WaitForJobs = waitForJobs });

            if (!completedSuccessfully)
            {
                throw new TimeoutException("Not all resources have deployed successfully within timeout");
            }
        }

        private IEnumerable<string> ReadManifestFiles(string workingDirectory)
        {
            var groupedFiles = GetGroupedYamlDirectories(workingDirectory).ToList();
            if (groupedFiles.Any())
            {
                return from file in groupedFiles
                       where fileSystem.FileExists(file)
                       select fileSystem.ReadFile(file);
            }

            return from file in GetManifestFileNames(workingDirectory)
                   where fileSystem.FileExists(file)
                   select fileSystem.ReadFile(file);
        }

        private IEnumerable<string> GetManifestFileNames(string workingDirectory)
        {
            var customResourceFileName =
                variables.Get(SpecialVariables.CustomResourceYamlFileName) ?? "customresource.yml";

            return new[]
            {
                "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
            }.Select(p => Path.Combine(workingDirectory, p));
        }

        private IEnumerable<string> GetGroupedYamlDirectories(string workingDirectory)
        {
            var groupedDirectories = variables.Get(SpecialVariables.GroupedYamlDirectories);
            return groupedDirectories != null
                ? groupedDirectories.Split(';').SelectMany(d => fileSystem.EnumerateFilesRecursively(Path.Combine(workingDirectory, d)))
                : Enumerable.Empty<string>();
        }

        private ResourceIdentifier GetConfigMap(string defaultNamespace)
        {
            if (!variables.GetFlag("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled"))
            {
                return null;
            }

            // Skip it if the user did not input configmap data
            if (!variables.GetIndexes("Octopus.Action.KubernetesContainers.ConfigMapData").Any())
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
            
            // Skip it if the user did not input secret data
            if (!variables.GetIndexes("Octopus.Action.KubernetesContainers.SecretData").Any())
            {
                return null;
            }

            var secretName = variables.Get("Octopus.Action.KubernetesContainers.ComputedSecretName");
            return string.IsNullOrEmpty(secretName) ? null : new ResourceIdentifier("Secret", secretName, defaultNamespace);
        }
    }
}