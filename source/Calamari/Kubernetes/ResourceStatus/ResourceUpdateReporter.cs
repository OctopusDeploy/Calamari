using System;
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
        void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses, int checkCount);
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

        public void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses, int checkCount)
        {
            var createdOrUpdatedResources = GetCreatedOrUpdatedResources(originalStatuses, newStatuses).ToList();
            foreach (var resource in createdOrUpdatedResources)
            {
                SendServiceMessage(resource, false, checkCount);
            }

            var removedResources = GetRemovedResources(originalStatuses, newStatuses).ToList();
            foreach (var resource in removedResources)
            {
                SendServiceMessage(resource, true, checkCount);
            }

            log.Verbose($"Resource Status Check: reported {createdOrUpdatedResources.Count} updates, {removedResources.Count} removals");
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

        private void SendServiceMessage(Resource resource, bool removed, int checkCount)
        {
            if (string.IsNullOrEmpty(resource.Namespace))
            {
                return;
            }
            
            var actionNumber = variables.Get("Octopus.Action.Number", string.Empty);
            var stepNumber = variables.Get("Octopus.Step.Number");
            var stepName = variables.Get("Octopus.Step.Name");
            
            if (actionNumber.IndexOf(".", StringComparison.Ordinal) > 0)
            {
                stepNumber = actionNumber;
                stepName = variables.Get("Octopus.Action.Name");
            }
            
            var parameters = new Dictionary<string, string>
            {
                {"type", "k8s-status"},
                {"actionId", variables.Get("Octopus.Action.Id")},
                {"stepName", $"Step {stepNumber}: {stepName}"},
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
                {"removed", removed.ToString()},
                {"checkCount", checkCount.ToString()}
            };

            var message = new ServiceMessage(SpecialVariables.ServiceMessageNames.ResourceStatus.Name, parameters);
            log.WriteServiceMessage(message);
        }
    }
}