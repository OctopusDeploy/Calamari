using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Checks the statuses of resources in a cluster and reports any updates to the server.
    /// </summary>
    public interface IResourceStatusChecker
    {
        /// <summary>
        /// Polling the resource status in a cluster and sends the update to the server,
        /// unitl the deployment timeout is met, or the deployment has succeeded or failed after stably.
        /// </summary>
        /// <returns>true if all resources have been deployed successfully, otherwise false</returns>
        bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            ICountdownTimer deploymentTimer, 
            ICountdownTimer stabilizationTimer, 
            IKubectl kubectl);
    }

    public enum DeploymentStatus
    {
        InProgress, Succeeded, Failed
    }
    
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private const int PollingIntervalSeconds = 2;

        private IDictionary<string, Resource> statuses = new Dictionary<string, Resource>();
        private DeploymentStatus deploymentStatus = DeploymentStatus.InProgress;

        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IResourceUpdateReporter reporter)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
        }
        
        public bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            ICountdownTimer deploymentTimer, 
            ICountdownTimer stabilizationTimer, 
            IKubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();

            deploymentTimer.Start();
            
            while (ShouldContinue(deploymentTimer, stabilizationTimer))
            {
                UpdateResourceStatuses(definedResources, kubectl);
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }

            return deploymentStatus == DeploymentStatus.Succeeded &&
                   (!stabilizationTimer.HasStarted() || stabilizationTimer.HasCompleted());
        }

        public void UpdateResourceStatuses(IEnumerable<ResourceIdentifier> definedResources, IKubectl kubectl)
        {
            var newResourceStatuses = resourceRetriever
                .GetAllOwnedResources(definedResources, kubectl)
                .ToDictionary(resource => resource.Uid, resource => resource);
    
            reporter.ReportUpdatedResources(statuses, newResourceStatuses);
            statuses = newResourceStatuses;
        }

        private bool ShouldContinue(ICountdownTimer deploymentTimer, ICountdownTimer stabilizationTimer)
        {
            if (statuses.Count == 0)
            {
                return true;
            }
            var newStatus = GetStatus(statuses.Values.ToList());
            var result = ShouldContinue(deploymentTimer, stabilizationTimer, deploymentStatus, newStatus);
            deploymentStatus = newStatus;
            return result;
        }
        
        internal static bool ShouldContinue(ICountdownTimer deploymentTimer, ICountdownTimer stabilizationTimer, DeploymentStatus oldStatus, DeploymentStatus newStatus)
        {
            if (deploymentTimer.HasCompleted() || stabilizationTimer.HasCompleted())
            {
                return false;
            }

            if (stabilizationTimer.HasStarted())
            {
                if (newStatus != oldStatus)
                {
                    stabilizationTimer.Reset();

                    if (newStatus != DeploymentStatus.InProgress)
                    {
                        stabilizationTimer.Start();
                    }
                }
            }
            else if (newStatus != DeploymentStatus.InProgress)
            {
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