using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Pod : Resource
    {
        public string Ready { get; }
        public int Restarts { get; }
        public string Status { get; }

        public Pod(JObject json, Options options) : base(json, options)
        {
            var phase = Field("$.status.phase");
            var initContainerStatuses = data
                .SelectToken("$.status.initContainerStatuses")
                ?.ToObject<ContainerStatus[]>() ?? new ContainerStatus[] { };
            var containerStatuses = data
                .SelectToken("$.status.containerStatuses")
                ?.ToObject<ContainerStatus[]>() ?? new ContainerStatus[] { };
            var ready = data
                .SelectToken("$.status.conditions[?(@.type == 'Ready')].status")
                ?.Value<string>() ?? string.Empty;
            
            Status = GetStatus(phase, initContainerStatuses, containerStatuses);

            var restartPolicy = FieldOrDefault("$.spec.restartPolicy", "Always");
            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus(phase, ready)
                : GetResourceStatus(phase, containerStatuses, ready, restartPolicy);

            var containers = containerStatuses.Length;
            var readyContainers = containerStatuses.Count(status => status.Ready);
            Ready = $"{readyContainers}/{containers}";
            Restarts = containerStatuses.Select(status => status.RestartCount).Sum();
        }

        static ResourceStatus GetLegacyResourceStatus(string phase, string ready)
        {
            switch (phase)
            {
                case "Failed":
                case "Unknown":
                    return ResourceStatus.Failed;
                case "Succeeded":
                    return ResourceStatus.Successful;
                case "Pending":
                    return ResourceStatus.InProgress;
                default:
                    return ready == "True" ? ResourceStatus.Successful : ResourceStatus.InProgress;
            }
        }

        // Aligns with gitops-engine getPodHealth.
        static ResourceStatus GetResourceStatus(string phase, ContainerStatus[] containerStatuses, string ready, string restartPolicy)
        {
            // gitops-engine flags image-pull/crash-loop errors as failed, but only for pods that are meant to
            // run continuously (RestartPolicy=Always) so that finite hook pods are not prematurely failed.
            if (restartPolicy == "Always" && containerStatuses.Any(HasImageOrCrashError))
            {
                return ResourceStatus.Failed;
            }

            switch (phase)
            {
                case "Pending":
                    return ResourceStatus.InProgress;
                case "Succeeded":
                    return ResourceStatus.Successful;
                case "Failed":
                    return ResourceStatus.Failed;
                case "Running":
                    if (restartPolicy == "OnFailure" || restartPolicy == "Never")
                    {
                        return ResourceStatus.InProgress;
                    }
                    if (ready == "True")
                    {
                        return ResourceStatus.Successful;
                    }
                    return containerStatuses.Any(status => status.LastState?.Terminated != null)
                        ? ResourceStatus.Failed
                        : ResourceStatus.InProgress;
                default:
                    return ResourceStatus.InProgress;
            }
        }

        static bool HasImageOrCrashError(ContainerStatus status)
        {
            var reason = status.State?.Waiting?.Reason;
            if (string.IsNullOrEmpty(reason))
            {
                return false;
            }
            return reason.StartsWith("Err") || reason.EndsWith("Error") || reason.EndsWith("BackOff");
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Pod>(lastStatus);
            return last.ResourceStatus != ResourceStatus || last.Status != Status;
        }

        private static string GetStatus(
            string phase, 
            ContainerStatus[] initContainerStatuses,
            ContainerStatus[] containerStatuses)
        {
            switch (phase)
            {
                case "Pending":
                    if (!initContainerStatuses.Any() && !containerStatuses.Any())
                    {
                        return "Pending";
                    }
                    return initContainerStatuses.All(HasCompleted) 
                        ? GetStatus(containerStatuses) 
                        : GetInitializingStatus(initContainerStatuses);
                case "Failed":
                case "Succeeded":
                    return GetReason(containerStatuses.FirstOrDefault());
                default:
                    return GetStatus(containerStatuses);
            }
        }

        private static string GetInitializingStatus(ContainerStatus[] initContainerStatuses)
        {
            var erroredContainer = initContainerStatuses.FirstOrDefault(HasError);
            if (erroredContainer != null)
            {
                return $"Init:{GetReason(erroredContainer)}";
            }

            var totalInit = initContainerStatuses.Length;
            var readyInit = initContainerStatuses.Where(HasCompleted).Count();
            return $"Init:{readyInit}/{totalInit}";
        }

        private static string GetStatus(ContainerStatus[] containerStatuses)
        {
            var erroredContainer = containerStatuses.FirstOrDefault(HasError);
            if (erroredContainer != null)
            {
                return GetReason(erroredContainer);
            }

            var containerWithReason = containerStatuses.FirstOrDefault(HasReason);
            if (containerWithReason != null)
            {
                return GetReason(containerWithReason);
            }

            return "Running";
        }
        
        private static string GetReason(ContainerStatus status)
        {
            // In real scenario this shouldn't happen, but we give it a default value just in case
            if (status == null)
            {
                return string.Empty;
            }
            
            if (status.State.Terminated != null)
            {
                return status.State.Terminated.Reason;
            }

            if (status.State.Waiting != null)
            {
                return status.State.Waiting.Reason;
            }

            return "Pending";
        }

        private static bool HasError(ContainerStatus status)
        {
            if (status.State.Terminated != null)
            {
                return status.State.Terminated.Reason != "Completed";
            }

            if (status.State.Waiting != null)
            {
                return status.State.Waiting.Reason != "PodInitializing" 
                    && status.State.Waiting.Reason != "ContainerCreating";
            }

            return false;
        }

        private static bool HasReason(ContainerStatus status)
        {
            return status.State.Terminated != null || status.State.Waiting != null;
        }
        
        private static bool HasCompleted(ContainerStatus status)
        {
            return status.State.Terminated != null && status.State.Terminated.Reason == "Completed";
        }
    }
}