using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus;

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
        return new Resource(result);
    }

    private IEnumerable<Resource> GetChildrenResources(Resource parentResource, ICommandLineRunner commandLineRunner)
    {
        var childKind = parentResource.ChildKind;
        if (string.IsNullOrEmpty(childKind))
        {
            return Enumerable.Empty<Resource>();
        }
        var result = kubectl.GetAll(childKind, parentResource.Namespace, commandLineRunner);
        var items = Resource.FromListResponse(result);
        return items.Where(item => item.Field<string>($"$.metadata.ownerReferences[0].uid") == parentResource.Uid);
    }
}