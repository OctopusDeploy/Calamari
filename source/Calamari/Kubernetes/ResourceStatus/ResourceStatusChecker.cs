using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IResourceStatusChecker
    {
        void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl);
    }
    
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private const int PollingIntervalSeconds = 2;

        private readonly IResourceRetriever resourceRetriever;
        private readonly ILog log;
        private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();
    
        // TODO change this to timeout
        private int count = 20;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.log = log;
        }
        
        public void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();
            while (!IsCompleted())
            {
                CheckStatus(definedResources, context, kubectl);
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }
        }

        public void CheckStatus(IEnumerable<ResourceIdentifier> definedResources, DeploymentContext context, Kubectl kubectl)
        {
            var newResourceStatuses = resourceRetriever
                .GetAllOwnedResources(definedResources, context, kubectl)
                .ToDictionary(resource => resource.Uid, resource => resource);
    
            var createdOrUpdatedResources = GetCreatedOrUpdatedResources(newResourceStatuses);
            var removedResources = GetRemovedResources(newResourceStatuses);
            resources = newResourceStatuses;
    
            foreach (var resource in createdOrUpdatedResources.Concat(removedResources))
            {
                log.Verbose($"Resource updated: {JsonConvert.SerializeObject(resource)}");
                SendServiceMessage(resource);
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

        private void SendServiceMessage(Resource resource)
        {
            // TODO: update this for database
            var parameters = new Dictionary<string, string>
            {
                {"type", "k8s-status"},
                {"data", JsonConvert.SerializeObject(resource)}
            };
    
            var message = new ServiceMessage("logData", parameters);
            log.WriteServiceMessage(message);
        }
    }
}