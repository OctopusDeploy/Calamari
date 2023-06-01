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
        /// until the deployment timeout is met, or the deployment has succeeded or failed.
        /// </summary>
        void CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            ITimer timer, 
            Kubectl kubectl,
            Options options);
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;
        private readonly ILog log;

        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IResourceUpdateReporter reporter, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
            this.log = log;
        }
        
        public void CheckStatusUntilCompletionOrTimeout(IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            ITimer timer,
            Kubectl kubectl,
            Options options)
        {
            var resourceStatuses = new Dictionary<string, Resource>();
            var definedResourceStatuses = new List<Resource>();
            var deploymentStatus = DeploymentStatus.InProgress;
            var checkCount = 0;
            var definedResources = resourceIdentifiers.ToList();
            
            timer.Start();
            
            do
            {
                var newDefinedResourceStatuses = resourceRetriever
                    .GetAllOwnedResources(definedResources, kubectl, options)
                    .ToList();
                var newResourceStatuses = newDefinedResourceStatuses
                    .SelectMany(IterateResourceTree)
                    .ToDictionary(resource => resource.Uid, resource => resource);

                var newDeploymentStatus = GetDeploymentStatus(newDefinedResourceStatuses, definedResources);

                reporter.ReportUpdatedResources(resourceStatuses, newResourceStatuses, ++checkCount);

                resourceStatuses = newResourceStatuses;
                definedResourceStatuses = newDefinedResourceStatuses;
                deploymentStatus = newDeploymentStatus;
                
                timer.WaitForInterval();
            }
            while (!timer.HasCompleted() && deploymentStatus == DeploymentStatus.InProgress);

            switch (deploymentStatus)
            {
                case DeploymentStatus.Succeeded:
                    log.Info("Resource status check completed successfully because all resources are deployed successfully");
                    break;
                case DeploymentStatus.InProgress:
                    LogInProgressResources(definedResourceStatuses, resourceStatuses, definedResources);
                    log.Error("Resource status check terminated because the timeout has been reached but some resources are still in progress");
                    break;
                case DeploymentStatus.Failed:
                    LogFailedResources(resourceStatuses);
                    log.Error("Resource status check terminated with errors because some resources have failed");
                    break;
            }
        }
        
        private static DeploymentStatus GetDeploymentStatus(List<Resource> resources, List<ResourceIdentifier> definedResources)
        {
            
            if (resources.All(resource => resource.ResourceStatus == ResourceStatus.Resources.ResourceStatus.Successful)
                && resources.Count == definedResources.Count)
            {
                return DeploymentStatus.Succeeded;
            }

            if (resources
                .Any(resource => resource.ResourceStatus == ResourceStatus.Resources.ResourceStatus.Failed))
            {
                return DeploymentStatus.Failed;
            }

            return DeploymentStatus.InProgress;
        }

        private void LogInProgressResources(List<Resource> definedResourceStatuses, Dictionary<string, Resource> resourceStatuses, List<ResourceIdentifier> definedResources)
        {
            var inProgress = resourceStatuses.Select(resource => resource.Value)
                .Where(resource => resource.ResourceStatus == Resources.ResourceStatus.InProgress)
                .ToList();
            if (inProgress.Any())
            {
                log.Verbose("The following resources are still in progress by the end of timeout:");
                foreach (var resource in inProgress)
                {
                    log.Verbose($" - {resource.Kind}/{resource.Name} in namespace {resource.Namespace}");
                }
            }

            foreach (var definedResource in definedResources)
            {
                if (!definedResourceStatuses.Any(resource =>
                        resource.Kind == definedResource.Kind 
                        && resource.Name == definedResource.Name 
                        && resource.Namespace == definedResource.Namespace))
                {
                    log.Verbose($"Resource {definedResource.Kind}/{definedResource.Name} in namespace {definedResource.Namespace} is not created by the end of timeout");
                }
            }
        }
        
        private void LogFailedResources(Dictionary<string, Resource> resources)
        {
            log.Verbose("The following resources have failed:");
            foreach (var resource in resources.Select(resource => resource.Value))
            {
                log.Verbose($" - {resource.Kind}/{resource.Name} in namespace {resource.Namespace}");
            }
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