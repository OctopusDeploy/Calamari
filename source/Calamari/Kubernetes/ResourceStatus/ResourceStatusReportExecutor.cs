#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public bool ReportStatus(string workingDirectory)
        {
            return StartReportingStatusInternal(workingDirectory, initialResourcesOnly: true) &&
                WaitForStatusReportingToComplete().GetAwaiter().GetResult();
        }

        public void StartReportingStatus(string workingDirectory)
        {
            StartReportingStatusInternal(workingDirectory, initialResourcesOnly: false);
        }

        private bool StartReportingStatusInternal(string workingDirectory, bool initialResourcesOnly)
        {
            var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
            // When the namespace on a target was set and then cleared, it's going to be "" instead of null
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                defaultNamespace = "default";
            }

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

            if (!definedResources.Any() &&
                initialResourcesOnly)
            {
                log.Verbose("No defined resources are found, skipping resource status check");
                return false;
            }

            log.Verbose("Performing resource status checks on the following resources:");
            foreach (var resourceIdentifier in definedResources)
            {
                log.Verbose($" - {resourceIdentifier.Kind}/{resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");
            }

            if (!definedResources.Any())
            {
                log.Info("Resource Status Check: Waiting for resources to be applied.");
            }

            DoResourceCheck(definedResources);
            return true;
        }

        public async Task<bool> WaitForStatusReportingToComplete()
        {
            return await statusChecker.CompleteCheckingStatus();
        }

        public void AddResources(ResourceIdentifier[] resources)
        {
            statusChecker.AddResources(resources);
        }

        private void DoResourceCheck(List<ResourceIdentifier> initialResources)
        {
            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var timer = timeoutSeconds == 0
                ? new InfiniteTimer(TimeSpan.FromSeconds(PollingIntervalSeconds)) as ITimer
                : new Timer(TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromSeconds(PollingIntervalSeconds));

            statusChecker.StartCheckingStatus(kubectl, initialResources, timer, new Options {  WaitForJobs = waitForJobs});
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
#endif