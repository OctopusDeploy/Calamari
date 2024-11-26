using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;
using ResourceStatusAttributes = Calamari.Kubernetes.SpecialVariables.ServiceMessages.ResourceStatus.Attributes;

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
                {ResourceStatusAttributes.Type, "k8s-status"},
                {ResourceStatusAttributes.ActionId, variables.Get("Octopus.Action.Id")},
                {ResourceStatusAttributes.StepName, $"Step {stepNumber}: {stepName}"},
                {ResourceStatusAttributes.TaskId, variables.Get(KnownVariables.ServerTask.Id)},
                {ResourceStatusAttributes.TargetId, variables.Get("Octopus.Machine.Id")},
                {ResourceStatusAttributes.TargetName, variables.Get("Octopus.Machine.Name")},
                {ResourceStatusAttributes.SpaceId, variables.Get("Octopus.Space.Id")},
                {ResourceStatusAttributes.Uuid, resource.Uid},
                {ResourceStatusAttributes.Group, resource.Group},
                {ResourceStatusAttributes.Version, resource.Version},
                {ResourceStatusAttributes.Kind, resource.Kind},
                {ResourceStatusAttributes.Name, resource.Name},
                {ResourceStatusAttributes.Namespace, resource.Namespace},
                {ResourceStatusAttributes.Status, resource.ResourceStatus.ToString()},
                {ResourceStatusAttributes.Data, JsonConvert.SerializeObject(resource)},
                {ResourceStatusAttributes.Removed, removed.ToString()},
                {ResourceStatusAttributes.CheckCount, checkCount.ToString()}
            };

            var message = new ServiceMessage(SpecialVariables.ServiceMessages.ResourceStatus.Name, parameters);
            log.WriteServiceMessage(message);
        }
    }
}