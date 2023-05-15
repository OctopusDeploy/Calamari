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
            
            Status = GetStatus(phase, initContainerStatuses, containerStatuses);
            
            ResourceStatus = phase == "Failed" || phase == "Unknown"
                ? ResourceStatus.Failed
                : phase == "Pending"
                    ? ResourceStatus.InProgress
                    : ResourceStatus.Successful;

            var containers = containerStatuses.Length;
            var readyContainers = containerStatuses.Count(status => status.Ready);
            Ready = $"{readyContainers}/{containers}";
            Restarts = containerStatuses.Select(status => status.RestartCount).Sum();
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