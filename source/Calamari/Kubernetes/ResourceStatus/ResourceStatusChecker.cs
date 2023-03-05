using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
    public enum ResourceAction
    {
        Created, Updated, Removed
    }
    
    public interface IResourceStatusChecker
    {
        void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context);
    }
    
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private readonly IResourceRetriever resourceRetriever;
        private readonly IServiceMessages serviceMessages;
        private readonly ILog log;
        private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();
    
        // TODO change this to timeout
        private int count = 20;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IServiceMessages serviceMessages, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.serviceMessages = serviceMessages;
            this.log = log;
        }
        
        public void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context)
        {
            var definedResources = resourceIdentifiers.ToList();
    
            resources = resourceRetriever
                .GetAllOwnedResources(definedResources, context)
                .ToDictionary(resource => resource.Uid, resource => resource);
    
            foreach (var resource in resources)
            {
                log.Info($"Found existing: {JsonConvert.SerializeObject(resource.Value)}");
                serviceMessages.Update(resource.Value);
            }
            
            while (!IsCompleted())
            {
                var newStatus = resourceRetriever
                    .GetAllOwnedResources(definedResources, context)
                    .ToDictionary(resource => resource.Uid, resource => resource);
    
                var diff = GetDiff(newStatus);
                resources = newStatus;
    
                foreach (var resourceDiff in diff)
                {
                    log.Info($"{(resourceDiff.Item1 == ResourceAction.Removed ? "Removed" : "")}{JsonConvert.SerializeObject(resourceDiff.Item2)}");
                    serviceMessages.Update(resourceDiff.Item2);
                }
                
                Thread.Sleep(2000);
            }
        }
    
        private bool IsCompleted()
        {
            return --count < 0 ||
                   (resources.Count > 0 && resources.All(resource => resource.Value.Status == Resources.ResourceStatus.Successful));
        }
    
        private IEnumerable<(ResourceAction, Resource)> GetDiff(IDictionary<string, Resource> newStatus)
        {
            var diff = new List<(ResourceAction, Resource)>();
            foreach (var status in newStatus)
            {
                if (!resources.ContainsKey(status.Key))
                {
                    diff.Add((ResourceAction.Created, status.Value));
                }
                else if (status.Value.HasUpdate(resources[status.Key]))
                {
                    diff.Add((ResourceAction.Updated, status.Value));
                }
            }
    
            foreach (var resourceEntry in resources)
            {
                if (!newStatus.ContainsKey(resourceEntry.Key))
                {
                    resourceEntry.Value.Removed = true;
                    diff.Add((ResourceAction.Removed, resourceEntry.Value));
                }
            }
    
            return diff;
        }
    }
}