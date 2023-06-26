using System.Collections.Generic;
using System.Linq;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Retrieves resources information from a kubernetes cluster
    /// </summary>
    public interface IResourceRetriever
    {
        /// <summary>
        /// Gets the resources identified by the resourceIdentifiers and all their owned resources
        /// </summary>
        IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, Kubectl kubectl, Options options);
    }
    
    public class ResourceRetriever : IResourceRetriever
    {
        private readonly IKubectlGet kubectlGet;

        public ResourceRetriever(IKubectlGet kubectlGet)
        {
            this.kubectlGet = kubectlGet;
        }
        
        /// <inheritdoc />
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, Kubectl kubectl, Options options)
        {
            var resources = resourceIdentifiers
                .Select(identifier => GetResource(identifier, kubectl, options))
                .Where(resource => resource != null)
                .ToList();

            foreach (var resource in resources)
            {
                resource.UpdateChildren(GetChildrenResources(resource, kubectl, options));
            }

            return resources;
        }

        private Resource GetResource(ResourceIdentifier resourceIdentifier, Kubectl kubectl, Options options)
        {
            var result = kubectlGet.Resource(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace, kubectl);
            return string.IsNullOrEmpty(result) ? null : ResourceFactory.FromJson(result, options);
        }
    
        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, Kubectl kubectl, Options options)
        {
            var childKind = parentResource.ChildKind;
            if (string.IsNullOrEmpty(childKind))
            {
                return Enumerable.Empty<Resource>();
            }

            var result = kubectlGet.AllResources(childKind, parentResource.Namespace, kubectl);
            var resources = ResourceFactory.FromListJson(result, options);
            return resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid))
                .Select(child =>
                {
                    child.UpdateChildren(GetChildrenResources(child, kubectl, options));
                    return child;
                }).ToList();
        }
    }
}