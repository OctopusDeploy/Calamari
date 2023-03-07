using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

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

        private IDictionary<string, Resource> statuses = new Dictionary<string, Resource>();
        private readonly Stopwatch stabilizationTimer = new Stopwatch();
        private bool stabilizing;
        private DeploymentStatus status = DeploymentStatus.InProgress;

        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IResourceUpdateReporter reporter)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
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
    
            reporter.ReportUpdatedResources(statuses, newResourceStatuses);
            statuses = newResourceStatuses;
        }

        private bool ShouldContinue(TimeSpan stabilizationTimeout)
        {
            var newStatus = GetStatus(statuses.Values.ToList());
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
                    if (newStatus != DeploymentStatus.InProgress)
                    {
                        stabilizing = true;
                        stabilizationTimer.Start();
                    }
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
    }
}