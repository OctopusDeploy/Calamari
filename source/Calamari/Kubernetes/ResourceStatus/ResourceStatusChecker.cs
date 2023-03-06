using System;
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
        bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl);
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
        private readonly ILog log;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.log = log;
        }
        
        public bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, DeploymentContext context, Kubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();
            var deploymentTimeout = TimeSpan.FromSeconds(context.DeploymentTimeoutSeconds);
            var stabilizationTimeout = TimeSpan.FromSeconds(context.StabilizationTimeoutSeconds);
            
            var deploymentTimer = new Stopwatch();
            deploymentTimer.Start();
            
            while (deploymentTimer.Elapsed <= deploymentTimeout && ShouldContinue(stabilizationTimeout))
            {
                UpdateResourceStatuses(definedResources, context, kubectl);
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }

            return stabilizing == false && status == DeploymentStatus.Succeeded;
        }

        public void UpdateResourceStatuses(IEnumerable<ResourceIdentifier> definedResources, DeploymentContext context, Kubectl kubectl)
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
                {"data", JsonConvert.SerializeObject(resource)}
            };
    
            var message = new ServiceMessage("logData", parameters);
            log.WriteServiceMessage(message);
        }
    }
}