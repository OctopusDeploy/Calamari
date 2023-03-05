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
        IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context);
    }
    
    public class ResourceRetriever : IResourceRetriever
    {
        private readonly IKubectl kubectl;
        
        public ResourceRetriever(IKubectl kubectl)
        {
            this.kubectl = kubectl;
        }
    
        /// <inheritdoc />
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context)
        {
            var resources = resourceIdentifiers
                .Select(identifier => GetResource(identifier, context))
                .ToList();
            var current = 0;
            while (current < resources.Count)
            {
                resources.AddRange(GetChildrenResources(resources[current], context));
                ++current;
            }
            return resources;
        }
    
        private Resource GetResource(ResourceIdentifier resourceIdentifier, DeploymentContext context)
        {
            var result = kubectl.Get(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace);
            return ResourceFactory.FromJson(result, context);
        }
    
        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, DeploymentContext context)
        {
            var childKind = parentResource.ChildKind;
            if (string.IsNullOrEmpty(childKind))
            {
                return Enumerable.Empty<Resource>();
            }
            var result = kubectl.GetAll(childKind, parentResource.Namespace);
            var resources = ResourceFactory.FromListJson(result, context);
            var children = resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid)).ToList();
            parentResource.Children = children;
            return children;
        }
    }
}