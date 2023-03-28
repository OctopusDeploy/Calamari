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
        /// <summary>
        /// Reports the difference of the originalStatuses and newStatuses to server.
        /// </summary>
        void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses);
    }
    
    /// <summary>
    /// <inheritdoc />
    /// </summary>
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
            foreach (var resource in GetCreatedOrUpdatedResources(originalStatuses, newStatuses))
            {
                SendServiceMessage(resource, false);
            }
            
            foreach (var resource in GetRemovedResources(originalStatuses, newStatuses))
            {
                SendServiceMessage(resource, true);
            }
        }
        
        private static IEnumerable<Resource> GetCreatedOrUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses)
        {
            return newStatuses.Where(resource =>
                    !originalStatuses.ContainsKey(resource.Key) ||
                    resource.Value.HasUpdate(originalStatuses[resource.Key]))
                .Select(resource => resource.Value);
        }

        private static IEnumerable<Resource> GetRemovedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses)
        {
            return originalStatuses
                .Where(resource => !newStatuses.ContainsKey(resource.Key))
                .Select(resource => resource.Value);
        }
        
        private void SendServiceMessage(Resource resource, bool removed)
        {
            var parameters = new Dictionary<string, string>
            {
                {"type", "k8s-status"},
                {"actionId", variables.Get("Octopus.Action.Id")},
                {"stepName", $"Step {variables.Get("Octopus.Step.Number")}: {variables.Get("Octopus.Step.Name")}"},
                {"taskId", variables.Get(KnownVariables.ServerTask.Id)},
                {"targetId", variables.Get("Octopus.Machine.Id")},
                {"targetName", variables.Get("Octopus.Machine.Name")},
                {"spaceId", variables.Get("Octopus.Space.Id")},
                {"uuid", resource.Uid},         
                {"kind", resource.Kind},
                {"name", resource.Name},
                {"namespace", resource.Namespace},
                {"status", resource.ResourceStatus.ToString()},
                {"data", JsonConvert.SerializeObject(resource)},
                {"removed", removed.ToString()}
            };
    
            var message = new ServiceMessage(SpecialVariables.KubernetesResourceStatusServiceMessageName, parameters);
            log.WriteServiceMessage(message);
        }
    }
}