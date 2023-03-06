using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IResourceStatusChecker
    {
        void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, Kubectl kubectl);
    }
    
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private const int PollingIntervalSeconds = 2;

        private readonly IResourceRetriever resourceRetriever;
        private readonly ILog log;
        private readonly IVariables variables;
        private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();
    
        // TODO change this to timeout
        private int count = 20;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, ILog log, IVariables variables)
        {
            this.resourceRetriever = resourceRetriever;
            this.log = log;
            this.variables = variables;
        }
        
        public void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, Kubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();
            while (!IsCompleted())
            {
                CheckStatus(definedResources, kubectl);
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }
        }

        public void CheckStatus(IEnumerable<ResourceIdentifier> definedResources, Kubectl kubectl)
        {
            var newResourceStatuses = resourceRetriever
                .GetAllOwnedResources(definedResources, kubectl)
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
            var parameters = new Dictionary<string, string>
            {
                {"actionId", variables.Get("Octopus.Action.Id")},
                {"taskId", variables.Get(KnownVariables.ServerTask.Id)},
                {"targetId", variables.Get("Octopus.Machine.Id")},
                {"uuid", resource.Uid},         
                {"kind", resource.Kind},
                {"name", resource.Name},
                {"namespace", resource.Namespace},
                {"status", resource.Status.ToString()},
                {"data", JsonConvert.SerializeObject(resource)}
            };
    
            var message = new ServiceMessage("k8s-status", parameters);
            log.WriteServiceMessage(message);
        }
    }
}