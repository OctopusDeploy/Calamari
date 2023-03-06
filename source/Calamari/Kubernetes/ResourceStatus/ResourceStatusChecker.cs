using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, TimeSpan deploymentTimeout, TimeSpan stabilizationTimeout, IKubectl kubectl);
    }

    public enum DeploymentStatus
    {
        InProgress, Succeeded, Failed
    }
    
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private const int PollingIntervalSeconds = 2;

        private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();
        private readonly Stopwatch stabilizationTimer = new Stopwatch();
        private bool stabilizing;
        private DeploymentStatus status = DeploymentStatus.InProgress;

        private readonly IResourceRetriever resourceRetriever;
        private readonly IVariables variables;
        private readonly ILog log;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IVariables variables, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.variables = variables;
            this.log = log;
        }
        
        public bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, TimeSpan deploymentTimeout, TimeSpan stabilizationTimeout, IKubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();

            var deploymentTimer = new Stopwatch();
            deploymentTimer.Start();
            
            while (deploymentTimer.Elapsed <= deploymentTimeout && ShouldContinue(stabilizationTimeout))
            {
                UpdateResourceStatuses(definedResources, kubectl);
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }

            return stabilizing == false && status == DeploymentStatus.Succeeded;
        }

        public void UpdateResourceStatuses(IEnumerable<ResourceIdentifier> definedResources, IKubectl kubectl)
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

        private bool ShouldContinue(TimeSpan stabilizationTimeout)
        {
            var newStatus = GetStatus(resources.Values.ToList());
            if (stabilizing)
            {
                if (stabilizationTimer.Elapsed > stabilizationTimeout)
                {
                    stabilizing = false;
                    return false;
                }

                if (newStatus != status)
                {
                    status = newStatus;
                    stabilizing = false;
                    stabilizationTimer.Reset();
                }
            }
            else if (newStatus != DeploymentStatus.InProgress)
            {
                status = newStatus;
                stabilizing = true;
                stabilizationTimer.Start();
            }

            return true;
        }

        private IEnumerable<Resource> GetCreatedOrUpdatedResources(IDictionary<string, Resource> newResourceStatuses)
        {
            var createdOrUpdated = new List<Resource>();
            foreach (var resource in newResourceStatuses)
            {
                if (!resources.ContainsKey(resource.Key) || resource.Value.HasUpdate(resources[resource.Key]))
                {
                    createdOrUpdated.Add(resource.Value);
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

        private DeploymentStatus GetStatus(List<Resource> resources)
        {
            if (resources.All(resource => resource.Status == Resources.ResourceStatus.Successful))
            {
                return DeploymentStatus.Succeeded;
            }

            if (resources.Count(resource => resource.Status == Resources.ResourceStatus.Failed) > 0)
            {
                return DeploymentStatus.Failed;
            }

            return DeploymentStatus.InProgress;
        }
        
        private void SendServiceMessage(Resource resource)
        {
            // TODO: update this for database
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
                {"data", JsonConvert.SerializeObject(resource)}
            };
    
            var message = new ServiceMessage(SpecialVariables.KubernetesResourceStatusServiceMessageName, parameters);
            log.WriteServiceMessage(message);
        }
    }
}