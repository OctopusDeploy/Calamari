using System.Collections.Generic;
using System.Linq;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Retrieves resources information from a kubernetes cluster
    /// </summary>
    public interface IResourceRetriever
    {
        /// <summary>
        /// Gets the resources identified by the resourceIdentifiers, and all their descendants as identified by the first element in the ownerReferences field
        /// </summary>
        IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, string cluster, string actionId);
    }
    
    public class ResourceRetriever : IResourceRetriever
    {
        private readonly IKubectl kubectl;
        
        public ResourceRetriever(IKubectl kubectl)
        {
            this.kubectl = kubectl;
        }
    
        /// <inheritdoc />
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, string cluster, string actionId)
        {
            var resources = resourceIdentifiers
                .Select(identifier => GetResource(identifier, cluster, actionId))
                .ToList();
            var current = 0;
            while (current < resources.Count)
            {
                resources.AddRange(GetChildrenResources(resources[current], cluster, actionId));
                ++current;
            }
            return resources;
        }
    
        private Resource GetResource(ResourceIdentifier resourceIdentifier, string cluster, string actionId)
        {
            var result = kubectl.Get(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace);
            return ResourceFactory.FromJson(result, cluster, actionId);
        }
    
        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, string cluster, string actionId)
        {
            var childKind = parentResource.ChildKind;
            if (string.IsNullOrEmpty(childKind))
            {
                return Enumerable.Empty<Resource>();
            }
            var result = kubectl.GetAll(childKind, parentResource.Namespace);
            var resources = ResourceFactory.FromListJson(result, cluster, actionId);
            var children = resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid)).ToList();
            parentResource.Children = children;
            return children;
        }
    }
}