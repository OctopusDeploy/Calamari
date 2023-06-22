#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IRunningResourceStatusCheck
    {
        Task<bool> WaitForCompletionOrTimeout();

        Task AddResources(ResourceIdentifier[] newResources);
    }

    public class RunningResourceStatusCheck : IRunningResourceStatusCheck
    {
        public delegate IRunningResourceStatusCheck Factory(
            TimeSpan timeout,
            Options options,
            IEnumerable<ResourceIdentifier> initialResources);

        internal const string MessageDeploymentSucceeded = "Resource status check completed successfully because all resources are deployed successfully";
        internal const string MessageDeploymentFailed = "Resource status check terminated with errors because some resources have failed";
        internal const string MessageInProgressAtTheEndOfTimeout = "Resource status check terminated because the timeout has been reached but some resources are still in progress";

        private readonly Func<ResourceStatusCheckTask> statusCheckTaskFactory;
        private readonly ILog log;

        private readonly TimeSpan timeout;
        private readonly Options options;

        private readonly SemaphoreSlim taskLock = new SemaphoreSlim(1, 1);
        private readonly HashSet<ResourceIdentifier> resources = new HashSet<ResourceIdentifier>();

        private CancellationTokenSource taskCancellationTokenSource;
        private Task<ResourceStatusCheckTask.Result> statusCheckTask;


        public RunningResourceStatusCheck(
            Func<ResourceStatusCheckTask> statusCheckTaskFactory,
            ILog log,
            TimeSpan timeout,
            Options options,
            IEnumerable<ResourceIdentifier> initialResources)
        {
            this.statusCheckTaskFactory = statusCheckTaskFactory;
            this.log = log;
            this.timeout = timeout;
            this.options = options;
            initialResources = initialResources.ToList();
            if (initialResources.Any())
            {
                log.Verbose("Resource Status Check: Performing resource status checks on the following resources:");
                log.LogResources(initialResources);
            }
            else
            {
                log.Verbose("Resource Status Check: Waiting for resources to be applied.");
            }
            statusCheckTask = RunNewStatusCheck(initialResources);
        }

        public async Task<bool> WaitForCompletionOrTimeout()
        {
            await taskLock.WaitAsync();
            try
            {
                var result = await statusCheckTask;

                switch (result.DeploymentStatus)
                {
                    case DeploymentStatus.Succeeded:
                        log.Info(MessageDeploymentSucceeded);
                        return true;
                    case DeploymentStatus.InProgress:
                        LogInProgressResources(result.ResourceStatuses);
                        LogNotCreatedResources(result.DefinedResourceStatuses, result.DefiniedResources);
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
            finally
            {
                taskLock.Release();
            }
        }

        public async Task AddResources(ResourceIdentifier[] newResources)
        {
            await taskLock.WaitAsync();

            try
            {
                taskCancellationTokenSource.Cancel();
                await statusCheckTask;
                statusCheckTask = RunNewStatusCheck(newResources);
                log.Verbose($"Resource Status Check: {newResources.Length} new resources have been added:");
                log.LogResources(newResources);
            }
            finally
            {
                taskLock.Release();
            }
        }

        private async Task<ResourceStatusCheckTask.Result> RunNewStatusCheck(IEnumerable<ResourceIdentifier> newResources)
        {
            taskCancellationTokenSource = new CancellationTokenSource();
            resources.UnionWith(newResources);
            return await statusCheckTaskFactory()
                .Run(resources, options, timeout, taskCancellationTokenSource.Token);
        }

        private void LogFailedResources(Dictionary<string, Resource> resourceDictionary)
        {
            log.Verbose("Resource Status Check: The following resources have failed:");
            log.LogResources(resourceDictionary.Values);
        }

        private void LogInProgressResources(Dictionary<string, Resource> resourceStatuses)
        {
            var inProgress = resourceStatuses.Values
                                             .Where(resource => resource.ResourceStatus == Resources.ResourceStatus.InProgress)
                                             .ToList();
            if (inProgress.Any())
            {
                log.Verbose("Resource Status Check: the following resources are still in progress by the end of the timeout:");
                log.LogResources(inProgress);
            }
        }

        private void LogNotCreatedResources(Resource[] definedResourceStatuses, IEnumerable<ResourceIdentifier> definedResources)
        {
            var notCreated = definedResources.Where(definedResource =>
                !definedResourceStatuses.Any(resource =>
                    resource.Kind == definedResource.Kind
                    && resource.Name == definedResource.Name
                    && resource.Namespace == definedResource.Namespace)).ToList();

            if (notCreated.Any())
            {
                log.Verbose("Resource Status Check: the following resource had not been created by the of the timeout:");
                log.LogResources(notCreated);
            }
        }
    }
}
#endif