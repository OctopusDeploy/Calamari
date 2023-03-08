using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus;

public enum ResourceAction
{
    Created, Updated, Removed
}

public interface IResourceStatusChecker
{
    void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, ICommandLineRunner commandLineRunner, ILog log);
}

public class ResourceStatusChecker : IResourceStatusChecker
{
    private readonly IResourceRetriever resourceRetriever;
    private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();

    // TODO remove this
    private int count = 20;
    
    public ResourceStatusChecker(IResourceRetriever resourceRetriever)
    {
        this.resourceRetriever = resourceRetriever;
    }
    
    public void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, ICommandLineRunner commandLineRunner, ILog log)
    {
        var definedResources = resourceIdentifiers.ToList();

        resources = resourceRetriever
            .GetAllOwnedResources(definedResources, commandLineRunner)
            .ToDictionary(resource => resource.Uid, resource => resource);

        foreach (var (_, resource) in resources)
        {
            log.Info($"Found existing: {JsonConvert.SerializeObject(resource)}");
        }
        
        while (!IsCompleted())
        {
            var newStatus = resourceRetriever
                .GetAllOwnedResources(definedResources, commandLineRunner)
                .ToDictionary(resource => resource.Uid, resource => resource);

            var diff = GetDiff(newStatus);
            resources = newStatus;

            foreach (var (action, resource) in diff)
            {
                var actionType = action switch
                {
                    ResourceAction.Created => "Created: ",
                    ResourceAction.Removed => "Removed: ",
                    ResourceAction.Updated => "Updated: ",
                    _ => throw new ArgumentOutOfRangeException()
                };

                log.Info($"{actionType}{JsonConvert.SerializeObject(resource)}");
            }
            
            Thread.Sleep(2000);
        }
    }

    private bool IsCompleted()
    {
        return resources.Count > 0 
               && resources.All(resource => resource.Value.Status == Resources.ResourceStatus.Successful)
            || --count < 0;
    }

    private IEnumerable<(ResourceAction, Resource)> GetDiff(IDictionary<string, Resource> newStatus)
    {
        var diff = new List<(ResourceAction, Resource)>();
        foreach (var (uid, resource) in newStatus)
        {
            if (!resources.ContainsKey(uid))
            {
                diff.Add((ResourceAction.Created, resource));
            }
            else if (resource.HasUpdate(resources[uid]))
            {
                diff.Add((ResourceAction.Updated, resource));
            }
        }

        foreach (var (id, resource) in resources)
        {
            if (!newStatus.ContainsKey(id))
            {
                diff.Add((ResourceAction.Removed, resource));
            }
        }

        return diff;
    }
}