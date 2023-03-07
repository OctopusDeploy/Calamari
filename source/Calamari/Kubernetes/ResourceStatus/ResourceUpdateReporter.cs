using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IResourceUpdateReporter
    {
        void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses);
    }
    
    public class ResourceUpdateReporter : IResourceUpdateReporter
    {
        private readonly IVariables variables;
        private readonly ILog log;
        
        public ResourceUpdateReporter(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }
        
        public void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses)
        {
            var createdOrUpdatedResources = GetCreatedOrUpdatedResources(originalStatuses, newStatuses);
            var removedResources = GetRemovedResources(originalStatuses, newStatuses);
            foreach (var resource in createdOrUpdatedResources.Concat(removedResources))
            {
                log.Verbose($"Resource updated: {JsonConvert.SerializeObject(resource)}");
                SendServiceMessage(resource);
            }
        }
        
        private IEnumerable<Resource> GetCreatedOrUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses)
        {
            var createdOrUpdated = new List<Resource>();
            foreach (var resource in newStatuses)
            {
                if (!originalStatuses.ContainsKey(resource.Key) || resource.Value.HasUpdate(originalStatuses[resource.Key]))
                {
                    createdOrUpdated.Add(resource.Value);
                }
            }
            return createdOrUpdated;
        }

        private IEnumerable<Resource> GetRemovedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses)
        {
            var removed = new List<Resource>();
            foreach (var resource in originalStatuses)
            {
                if (!newStatuses.ContainsKey(resource.Key))
                {
                    resource.Value.Removed = true;
                    removed.Add(resource.Value);
                }
            }
            return removed;
        }
        
        private void SendServiceMessage(Resource resource)
        {
            var parameters = new Dictionary<string, string>
            {
                {"type", "k8s-status"},
                {"actionId", variables.Get("Octopus.Action.Id")},
                {"taskId", variables.Get(KnownVariables.ServerTask.Id)},
                {"targetId", variables.Get("Octopus.Machine.Id")},
                {"environmentId", variables.Get(DeploymentEnvironment.Id)},
                {"spaceId", variables.Get("Octopus.Space.Id")},
                {"tenantId", variables.Get(DeploymentVariables.Tenant.Id)},
                {"projectId", variables.Get(ProjectVariables.Id)},
                {"uuid", resource.Uid},         
                {"kind", resource.Kind},
                {"name", resource.Name},
                {"namespace", resource.Namespace},
                {"status", resource.Status.ToString()},
                {"data", JsonConvert.SerializeObject(resource)},
                {"removed", resource.Removed.ToString()}
            };
    
            var message = new ServiceMessage(SpecialVariables.KubernetesResourceStatusServiceMessageName, parameters);
            log.WriteServiceMessage(message);
        }
    }
}