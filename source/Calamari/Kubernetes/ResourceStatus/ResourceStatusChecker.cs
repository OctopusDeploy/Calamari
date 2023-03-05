using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
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
            while (!IsCompleted())
            {
                var newResourceStatuses = resourceRetriever
                    .GetAllOwnedResources(definedResources, context)
                    .ToDictionary(resource => resource.Uid, resource => resource);
    
                var createdOrUpdatedResources = GetCreatedOrUpdatedResources(newResourceStatuses);
                var removedResources = GetRemovedResources(newResourceStatuses);
                resources = newResourceStatuses;
    
                foreach (var resource in createdOrUpdatedResources)
                {
                    log.Verbose($"{JsonConvert.SerializeObject(resource)}");
                    serviceMessages.Update(resource);
                }

                foreach (var resource in removedResources)
                {
                    log.Verbose($"Removed: {JsonConvert.SerializeObject(resource)}");
                    serviceMessages.Update(resource);
                }
                
                Thread.Sleep(2000);
            }
        }
    
        private bool IsCompleted()
        {
            return --count < 0 ||
                   (resources.Count > 0 && resources.All(resource => resource.Value.Status == Resources.ResourceStatus.Successful));
        }
    
        private IEnumerable<Resource> GetCreatedOrUpdatedResources(IDictionary<string, Resource> newResourceStatuses)
        {
            var createdOrUpdated = new List<Resource>();
            foreach (var status in newResourceStatuses)
            {
                if (!resources.ContainsKey(status.Key) || status.Value.HasUpdate(resources[status.Key]))
                {
                    createdOrUpdated.Add(status.Value);
                }
            }
            return createdOrUpdated;
        }

        private IEnumerable<Resource> GetRemovedResources(IDictionary<string, Resource> newResourceStatuses)
        {
            var removed = new List<Resource>();
            foreach (var resourceEntry in resources)
            {
                if (!newResourceStatuses.ContainsKey(resourceEntry.Key))
                {
                    resourceEntry.Value.Removed = true;
                    removed.Add(resourceEntry.Value);
                }
            }
            return removed;
        }
    }
}