using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes
{
    public interface IKubernetesManifestNamespaceResolver
    {
        string ResolveNamespace(YamlMappingNode rootManifestNode, IVariables variables);
        string GetImplicitNamespace(IVariables variables);
    }

    public class KubernetesManifestNamespaceResolver : IKubernetesManifestNamespaceResolver
    {
        readonly IApiResourceScopeLookup apiResourceScopeLookup;
        readonly ILog log;

        public KubernetesManifestNamespaceResolver(IApiResourceScopeLookup apiResourceScopeLookup, ILog log)
        {
            this.apiResourceScopeLookup = apiResourceScopeLookup;
            this.log = log;
        }
        
        public string ResolveNamespace(YamlMappingNode rootManifestNode, IVariables variables)
        {
            var metadataNode = rootManifestNode.GetChildNode<YamlMappingNode>("metadata");
            
            var ns = GetNamespace(metadataNode, variables);

            var apiResourceIdentifier = GetApiResourceIdentifier(rootManifestNode);
            if (apiResourceScopeLookup.TryGetIsNamespaceScoped(apiResourceIdentifier, out var isNamespaceScoped))
            {
                //if the resource is cluster scoped, remove the namespace
                if (!isNamespaceScoped)
                {
                    ns = null;
                }
            }
            else
            {
                //if we can't determine the resource scope, log a verbose message
                log.Verbose($"Unable to determine if resource type {apiResourceIdentifier} is namespaced. Resources will be treated as namespaced.");
            }

            return ns;
        }

        public string GetImplicitNamespace(IVariables variables)
        {
            //we check to see if there is an explicit helm namespace defined first
            //then fallback on the action/target default namespace
            //otherwise fallback on default
            return variables.Get(SpecialVariables.Helm.Namespace) ?? variables.Get(SpecialVariables.Namespace) ?? "default";
        }

        string GetNamespace(YamlMappingNode metadataNode, IVariables variables)
        {
            //if we have a namespace node and it's not empty, just use that value
            var ns = metadataNode.GetChildNodeIfExists<YamlScalarNode>("namespace")?.Value;
            if (!string.IsNullOrWhiteSpace(ns))
            {
                return ns;
            }
            
            //if we don't, then fallback on variables
            return GetImplicitNamespace(variables);
        }
        
        static ApiResourceIdentifier GetApiResourceIdentifier(YamlMappingNode node)
        {
            var apiVersion = node.GetChildNodeIfExists<YamlScalarNode>("apiVersion")?.Value;
            var kind = node.GetChildNodeIfExists<YamlScalarNode>("kind")?.Value;
            return new ApiResourceIdentifier(apiVersion, kind);
        }
    }
}