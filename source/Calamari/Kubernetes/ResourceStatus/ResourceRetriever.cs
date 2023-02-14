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
        var resources = new List<ResourceIdentifier> { resourceIdentifier };
        var statuses = new List<Resource>();
        
        var current = 0;
        while (current < resources.Count)
        {
            var status = GetStatus(resources[current], commandLineRunner);
            statuses.Add(status);
            // Add the UID field before trying to fetch children
            resources[current] = GetResource(status);

            var children = GetChildrenStatuses(resources[current], commandLineRunner);
            resources.AddRange(children.Select(GetResource));

            ++current;
        }

        return statuses;
    }

    Resource GetStatus(ResourceIdentifier resourceIdentifier, ICommandLineRunner commandLineRunner)
    {
        var result = kubectl.Get(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace, commandLineRunner);
        return new Resource(result);
    }
    
    IEnumerable<Resource> GetChildrenStatuses(ResourceIdentifier resourceIdentifier, ICommandLineRunner commandLineRunner)
    {
        var childKind = GetChildKind(resourceIdentifier);
        if (string.IsNullOrEmpty(childKind))
        {
            return Enumerable.Empty<Resource>();
        }
        var result = kubectl.GetAll(childKind, resourceIdentifier.Namespace, commandLineRunner);
        var data = JObject.Parse(result);
        var items = data.SelectTokens($"$.items[?(@.metadata.ownerReferences[0].uid == '{resourceIdentifier.Uid}')]");
        return items.Select(item => new Resource((JObject)item));
    }

    ResourceIdentifier GetResource(Resource resource)
    {
        return new ResourceIdentifier
        {
            Kind = resource.Kind,
            Name = resource.Name,
            Namespace = resource.Namespace,
            Uid = resource.Uid
        };
    }
    
    string GetChildKind(ResourceIdentifier resourceIdentifier)
    {
        switch (resourceIdentifier.Kind)
        {
            case "Deployment":
                return "ReplicaSet";
            case "ReplicaSet":
                return "Pod";
        }

        return "";
    }
}