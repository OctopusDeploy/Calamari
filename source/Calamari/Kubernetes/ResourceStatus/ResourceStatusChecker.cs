using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
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
        /// Polls the resource status in a cluster and sends the update to the server
        /// until the deployment timeout is met, or the deployment has succeeded or failed stably.
        /// </summary>
        /// <returns>true if all resources have been deployed successfully, otherwise false</returns>
        bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            IStabilizingTimer stabilizingTimer, 
            Kubectl kubectl);
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private const int PollingIntervalSeconds = 2;

        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;
        private readonly ILog log;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IResourceUpdateReporter reporter, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
            this.log = log;
        }
        
        public bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            IStabilizingTimer stabilizingTimer,
            Kubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();

            var resourceStatuses = new Dictionary<string, Resource>();
            var deploymentStatus = DeploymentStatus.InProgress;
            var shouldContinue = true;
            var countOfChecks = 0;
            
            stabilizingTimer.Start();
            
            while (shouldContinue)
            {
                var newResourceStatuses = resourceRetriever
                    .GetAllOwnedResources(definedResources, kubectl)
                    .SelectMany(IterateResourceTree)
                    .ToDictionary(resource => resource.Uid, resource => resource);

                var newDeploymentStatus = GetDeploymentStatus(newResourceStatuses.Values.ToList());

                reporter.ReportUpdatedResources(resourceStatuses, newResourceStatuses, ++countOfChecks);
                
                shouldContinue = stabilizingTimer.ShouldContinue(deploymentStatus, newDeploymentStatus);

                resourceStatuses = newResourceStatuses;
                deploymentStatus = newDeploymentStatus;
                
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }

            if (stabilizingTimer.IsStabilizing())
            {
                switch (deploymentStatus)
                {
                    case DeploymentStatus.Succeeded:
                        log.Verbose("Resource status check terminated during stabilization period with all resources being successful");
                        break;
                    case DeploymentStatus.Failed:
                        log.Verbose("Resource status check terminated during stabilization period with some failed resources");
                        break;
                    default:
                        break;
                }

                return false;
            }

            switch (deploymentStatus)
            {
                case DeploymentStatus.Succeeded:
                    log.Verbose("Resource status check completed successfully because all resources are deployed successfully and have stabilized");
                    return true;
                case DeploymentStatus.InProgress:
                    log.Verbose("Resource status check terminated because the execution timeout has been reached but some resources are still in progress");
                    return false;
                case DeploymentStatus.Failed:
                    log.Verbose("Resource status check terminated with errors because some resources have failed and did not recover during stabilization period");
                    return false;
                default:
                    return false;
            }
        }

        private static DeploymentStatus GetDeploymentStatus(List<Resource> resources)
        {
            if (resources.All(resource => resource.ResourceStatus == Resources.ResourceStatus.Successful))
            {
                return DeploymentStatus.Succeeded;
            }

            if (resources.Any(resource => resource.ResourceStatus == Resources.ResourceStatus.Failed))
            {
                return DeploymentStatus.Failed;
            }

            return DeploymentStatus.InProgress;
        }

        private static IEnumerable<Resource> IterateResourceTree(Resource root)
        {
            foreach (var resource in root.Children ?? Enumerable.Empty<Resource>())
            {
                foreach (var child in IterateResourceTree(resource))
                {
                    yield return child;
                }
            }
            yield return root;
        }
    }
}