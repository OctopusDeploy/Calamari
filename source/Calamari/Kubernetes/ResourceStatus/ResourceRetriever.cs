using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus;

/// <summary>
/// Retrieves resources information from a kubernetes cluster
/// </summary>
public interface IResourceRetriever
{
    /// <summary>
    /// Gets the resource identified by resourceIdentifier, and all its descendants as identified by the first element in the ownerReferences field
    /// </summary>
    IEnumerable<Resource> GetAllOwnedResources(ResourceIdentifier resourceIdentifier, ICommandLineRunner commandLineRunner);
}

public class ResourceRetriever : IResourceRetriever
{
    private readonly IKubectl kubectl;
    
    public ResourceRetriever(IKubectl kubectl)
    {
        this.kubectl = kubectl;
    }

    /// <inheritdoc />
    public IEnumerable<Resource> GetAllOwnedResources(ResourceIdentifier resourceIdentifier, ICommandLineRunner commandLineRunner)
    {
        var rootResource = GetResource(resourceIdentifier, commandLineRunner);
        var resources = new List<Resource> {rootResource};
        
        var current = 0;
        while (current < resources.Count)
        {
            var children = GetChildrenResources(resources[current], commandLineRunner);
            resources.AddRange(children);

            ++current;
        }

        return resources;
    }

    private Resource GetResource(ResourceIdentifier resourceIdentifier, ICommandLineRunner commandLineRunner)
    {
        var result = kubectl.Get(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace, commandLineRunner);
        return ResourceFactory.FromJson(result);
    }

    private IEnumerable<Resource> GetChildrenResources(Resource parentResource, ICommandLineRunner commandLineRunner)
    {
        var childKind = parentResource.ChildKind;
        if (string.IsNullOrEmpty(childKind))
        {
            return Enumerable.Empty<Resource>();
        }
        var result = kubectl.GetAll(childKind, parentResource.Namespace, commandLineRunner);
        var items = ResourceFactory.FromListJson(result);
        return items.Where(item => item.Field($"$.metadata.ownerReferences[0].uid") == parentResource.Uid);
    }
}