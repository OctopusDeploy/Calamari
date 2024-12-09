using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceFinder
    {
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;

        public ResourceFinder(IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
        }

        public IEnumerable<ResourceIdentifier> FindResources(string workingDirectory)
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
            if (secret.HasValue)
            {
                definedResources.Add(secret.Value);
            }

            var configMap = GetConfigMap(defaultNamespace);
            if (configMap.HasValue)
            {
                definedResources.Add(configMap.Value);
            }

            return definedResources;
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
                "secret.yml", "feedsecrets.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml", "configmap.yml"
            }.Select(p => Path.Combine(workingDirectory, p));
        }

        private IEnumerable<string> GetGroupedYamlDirectories(string workingDirectory)
        {
            var groupedDirectories = variables.Get(SpecialVariables.GroupedYamlDirectories);
            return groupedDirectories != null
                ? groupedDirectories.Split(';').SelectMany(d => fileSystem.EnumerateFilesRecursively(Path.Combine(workingDirectory, d)))
                : Enumerable.Empty<string>();
        }

        private ResourceIdentifier? GetConfigMap(string defaultNamespace)
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
            return string.IsNullOrEmpty(configMapName) ? (ResourceIdentifier?)null : new ResourceIdentifier(SupportedResourceGroupVersionKinds.ConfigMapV1, configMapName, defaultNamespace);
        }

        private ResourceIdentifier? GetSecret(string defaultNamespace)
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
            return string.IsNullOrEmpty(secretName) ? (ResourceIdentifier?)null : new ResourceIdentifier(SupportedResourceGroupVersionKinds.SecretV1, secretName, defaultNamespace);
        }
    }
}