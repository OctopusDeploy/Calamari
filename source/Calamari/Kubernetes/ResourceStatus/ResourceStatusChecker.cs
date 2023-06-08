#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        /// <returns>true if all defined resources have been deployed successfully, otherwise false</returns>
        Task<bool> CheckStatusUntilCompletionOrTimeout(IKubectl kubectl, IEnumerable<ResourceIdentifier> initialResources, ITimer timer,
            Options options);

        void StartCheckingStatus(
            IKubectl kubectl,
            IEnumerable<ResourceIdentifier> initialResources,
            ITimer timer,
            Options options);

        Task<bool> CompleteCheckingStatus();

        void AddResources(IEnumerable<ResourceIdentifier> newResources);
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        internal const string MessageDeploymentSucceeded = "Resource status check completed successfully because all resources are deployed successfully";
        internal const string MessageDeploymentFailed = "Resource status check terminated with errors because some resources have failed";
        internal const string MessageInProgressAtTheEndOfTimeout = "Resource status check terminated because the timeout has been reached but some resources are still in progress";

        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;
        private readonly ILog log;
        private ITimer timer;
        private readonly HashSet<ResourceIdentifier> resources = new HashSet<ResourceIdentifier>();
        private readonly object resourceLock = new object();
        private bool isCheckingStatus;
        private bool waitingForMoreResources;
        private Task<ResourceStatusTaskResult> statusCheckTask;

        public ResourceStatusChecker(
            IResourceRetriever resourceRetriever,
            IResourceUpdateReporter reporter,
            ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
            this.log = log;
        }

        public async Task<bool> CheckStatusUntilCompletionOrTimeout(IKubectl kubectl,
            IEnumerable<ResourceIdentifier> initialResources,
            ITimer timer,
            Options options)
        {
            StartCheckingStatus(kubectl, initialResources, timer, options);
            return await CompleteCheckingStatus();
        }

        public void StartCheckingStatus(
            IKubectl kubectl,
            IEnumerable<ResourceIdentifier> initialResources,
            ITimer timer,
            Options options)
        {
            if (isCheckingStatus)
                throw new InvalidOperationException(
                    "method CheckStatusUntilCompletionOrTimeout must only be called once.");

            isCheckingStatus = true;

            AddResourcesInternal(initialResources);

            this.timer = timer;

            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Unable to set KubeCtl");
            }

            var checkCount = 0;

            waitingForMoreResources = true;
            timer.Start();
            statusCheckTask = Task.Run(() =>
            {
                var result = new ResourceStatusTaskResult();
                do
                {
                    ResourceIdentifier[] definedResources;
                    bool stillWaitingForMoreResources;
                    lock (resourceLock)
                    {
                        stillWaitingForMoreResources = waitingForMoreResources;
                        definedResources = resources.ToArray();
                    }

                    if (definedResources.Any())
                    {
                        var definedResourceStatuses = resourceRetriever
                                                         .GetAllOwnedResources(definedResources, kubectl, options)
                                                         .ToArray();
                        var resourceStatuses = definedResourceStatuses
                                                  .SelectMany(IterateResourceTree)
                                                  .ToDictionary(resource => resource.Uid, resource => resource);

                        var deploymentStatus = stillWaitingForMoreResources
                            ? DeploymentStatus.InProgress
                            : GetDeploymentStatus(definedResourceStatuses, definedResources);

                        reporter.ReportUpdatedResources(result.ResourceStatuses, resourceStatuses, ++checkCount);

                        result = new ResourceStatusTaskResult(
                            definedResources,
                            definedResourceStatuses,
                            resourceStatuses,
                            deploymentStatus);
                    }

                    timer.WaitForInterval();
                } while (!timer.HasCompleted() && result.DeploymentStatus == DeploymentStatus.InProgress);
                isCheckingStatus = false;
                return result;
            });
        }

        public void AddResources(IEnumerable<ResourceIdentifier> newResources)
        {
            var newResourcesList = newResources.ToList();
            log.Info($"Resource Status Check: {newResourcesList.Count} new resources have been added.");
            AddResourcesInternal(newResourcesList);
            timer?.Restart();
        }

        public async Task<bool> CompleteCheckingStatus()
        {
            if (statusCheckTask == null)
                throw new InvalidOperationException(
                    "Call 'StartCheckingStatus()' before calling 'CompleteCheckingStatus'");

            waitingForMoreResources = false;

            var result = await statusCheckTask;

            switch (result.DeploymentStatus)
            {
                case DeploymentStatus.Succeeded:
                    log.Info(MessageDeploymentSucceeded);
                    return true;
                case DeploymentStatus.InProgress:
                    LogInProgressResources(result.DefinedResourceStatuses, result.ResourceStatuses, result.DefiniedResources);
                    log.Error(MessageInProgressAtTheEndOfTimeout);
                    return false;
                case DeploymentStatus.Failed:
                    LogFailedResources(result.ResourceStatuses);
                    log.Error(MessageDeploymentFailed);
                    return false;
                default:
                    return false;
            }
        }

        private static DeploymentStatus GetDeploymentStatus(Resource[] resources, ResourceIdentifier[] definedResources)
        {

            if (resources.All(resource => resource.ResourceStatus == ResourceStatus.Resources.ResourceStatus.Successful)
                && resources.Length == definedResources.Length)
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

        private void AddResourcesInternal(IEnumerable<ResourceIdentifier> newResources)
        {
            foreach (var resource in newResources)
            {
                lock (resourceLock)
                {
                    resources.Add(resource);
                }
            }
        }

        private void LogInProgressResources(Resource[] definedResourceStatuses, Dictionary<string, Resource> resourceStatuses, IEnumerable<ResourceIdentifier> definedResources)
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

        private class ResourceStatusTaskResult
        {
            public ResourceStatusTaskResult()
                : this(
                    Array.Empty<ResourceIdentifier>(),
                    Array.Empty<Resource>(),
                    new Dictionary<string, Resource>(),
                    DeploymentStatus.InProgress)
            {
            }

            public ResourceStatusTaskResult(
                ResourceIdentifier[] definedResources,
                Resource[] definedResourceStatuses,
                Dictionary<string, Resource> resourceStatuses,
                DeploymentStatus deploymentStatus)
            {
                DefiniedResources = definedResources;
                DefinedResourceStatuses = definedResourceStatuses;
                ResourceStatuses = resourceStatuses;
                DeploymentStatus = deploymentStatus;
            }

            public ResourceIdentifier[] DefiniedResources { get; }
            public Resource[] DefinedResourceStatuses { get; }
            public Dictionary<string, Resource> ResourceStatuses { get; }
            public DeploymentStatus DeploymentStatus { get; }
        }
    }
}
#endif