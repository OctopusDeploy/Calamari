using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IResourceFinder
    {
        IEnumerable<ResourceIdentifier> FindResources(string workingDirectory);
    }

    public class ResourceFinder : IResourceFinder
    {
        readonly IVariables variables;
        readonly IManifestRetriever manifestRetriever;
        readonly IKubernetesManifestNamespaceResolver namespaceResolver;
        readonly ILog log;

        public ResourceFinder(IVariables variables, IManifestRetriever manifestRetriever, IKubernetesManifestNamespaceResolver namespaceResolver, ILog log)
        {
            this.variables = variables;
            this.manifestRetriever = manifestRetriever;
            this.namespaceResolver = namespaceResolver;
            this.log = log;
        }

        public IEnumerable<ResourceIdentifier> FindResources(string workingDirectory)
        {
            //discover resources in the manifests
            var manifests = manifestRetriever.GetManifests(workingDirectory).ToList();
            var definedResources = KubernetesYaml.GetDefinedResources(manifests, namespaceResolver, variables, log).ToList();

            //look for the computed configmaps and secrets
            var defaultNamespace = namespaceResolver.GetImplicitNamespace(variables);
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

        ResourceIdentifier? GetConfigMap(string defaultNamespace)
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

        ResourceIdentifier? GetSecret(string defaultNamespace)
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