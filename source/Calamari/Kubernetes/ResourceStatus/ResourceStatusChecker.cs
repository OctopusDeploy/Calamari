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

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IResourceUpdateReporter reporter)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
        }
        
        public bool CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            IStabilizingTimer stabilizingTimer,
            Kubectl kubectl)
        {
            var definedResources = resourceIdentifiers.ToList();

            var resourceStatuses = new Dictionary<string, Resource>();
            var deploymentStatus = DeploymentStatus.InProgress;
            var shouldContinue = true;

            stabilizingTimer.Start();
            
            while (shouldContinue)
            {
                var newResourceStatuses = resourceRetriever
                    .GetAllOwnedResources(definedResources, kubectl)
                    .SelectMany(IterateResourceTree)
                    .ToDictionary(resource => resource.Uid, resource => resource);

                var newDeploymentStatus = GetDeploymentStatus(newResourceStatuses.Values.ToList());

                reporter.ReportUpdatedResources(resourceStatuses, newResourceStatuses);
                
                shouldContinue = stabilizingTimer.ShouldContinue(deploymentStatus, newDeploymentStatus);

                resourceStatuses = newResourceStatuses;
                deploymentStatus = newDeploymentStatus;
                
                Thread.Sleep(PollingIntervalSeconds * 1000);
            }

            return deploymentStatus == DeploymentStatus.Succeeded && !stabilizingTimer.IsStabilizing();
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