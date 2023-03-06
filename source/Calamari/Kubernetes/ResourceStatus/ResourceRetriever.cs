using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
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
        IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl);
    }
    
    public class ResourceRetriever : IResourceRetriever
    {
        /// <inheritdoc />
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl)
        {
            var resources = resourceIdentifiers
                .Select(identifier => GetResource(identifier, context, kubectl))
                .ToList();
            var current = 0;
            while (current < resources.Count)
            {
                resources.AddRange(GetChildrenResources(resources[current], context, kubectl));
                ++current;
            }
            return resources;
        }
    
        private Resource GetResource(ResourceIdentifier resourceIdentifier, DeploymentContext context, Kubectl kubectl)
        {
            var result = kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", resourceIdentifier.Kind, resourceIdentifier.Name, "-o json", $"-n {resourceIdentifier.Namespace}"
            }).Join("");
            return ResourceFactory.FromJson(result, context);
        }
    
        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, DeploymentContext context, Kubectl kubectl)
        {
            var childKind = parentResource.ChildKind;
            if (string.IsNullOrEmpty(childKind))
            {
                return Enumerable.Empty<Resource>();
            }
            
            var result = kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", parentResource.Kind, "-o json", $"-n {parentResource.Namespace}"
            }).Join("");
            
            var resources = ResourceFactory.FromListJson(result, context);
            var children = resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid)).ToList();
            parentResource.Children = children;
            return children;
        }
    }
}