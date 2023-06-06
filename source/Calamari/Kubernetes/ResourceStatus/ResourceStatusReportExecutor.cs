using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
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

        public ResourceStatusReportExecutor(IVariables variables, ILog log, ICalamariFileSystem fileSystem, IResourceStatusChecker statusChecker)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.statusChecker = statusChecker;
        }

        public void ReportStatus(string workingDirectory, ICommandLineRunner commandLineRunner, Dictionary<string, string> environmentVars)
        {
            var customKubectlExecutable = variables.Get(SpecialVariables.CustomKubectlExecutable);
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
            }

            log.Verbose("Performing resource status checks on the following resources:");
            foreach (var resourceIdentifier in definedResources)
            {
                log.Verbose($" - {resourceIdentifier.Kind}/{resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");
            }

            var kubectl = new Kubectl(customKubectlExecutable, log, commandLineRunner, workingDirectory, environmentVars);
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
            foreach (var file in GetManifestFileNames())
            {
                var filePath = Path.Combine(workingDirectory, file);
                if (fileSystem.FileExists(filePath)) yield return fileSystem.ReadFile(filePath);
            }

            foreach (var file in GetGroupedYamlDirectories(workingDirectory))
            {
                if (fileSystem.FileExists(file)) yield return fileSystem.ReadFile(file);
            }
        }

        private IEnumerable<string> GetManifestFileNames()
        {
            var customResourceFileName =
                variables.Get(SpecialVariables.CustomResourceYamlFileName) ?? "customresource.yml";

            return new[]
            {
                "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
            };
        }

        private IEnumerable<string> GetGroupedYamlDirectories(string workingDirectory)
        {
            var groupedDirectories = variables.Get(SpecialVariables.GroupedYamlDirectories);
            return groupedDirectories != null
                ? groupedDirectories.Split(';').SelectMany(d => fileSystem.EnumerateFiles(Path.Combine(workingDirectory, d)))
                : Enumerable.Empty<string>();
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