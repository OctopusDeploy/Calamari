using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Pod : Resource
    {
        public string Status { get; }
        public override ResourceStatus ResourceStatus { get; }
    
        public Pod(JObject json) : base(json)
        {
            var phase = Field("$.status.phase");
            var initContainerStatuses = data
                .SelectToken("$.status.initContainerStatuses")
                ?.ToObject<ContainerStatus[]>() ?? Array.Empty<ContainerStatus>();
            var containerStatuses = data
                .SelectToken("$.status.containerStatuses")
                ?.ToObject<ContainerStatus[]>() ?? Array.Empty<ContainerStatus>();

            (Status, ResourceStatus) = GetStatus(phase, initContainerStatuses, containerStatuses);
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Pod>(lastStatus);
            return last.ResourceStatus != ResourceStatus || last.Status != Status;
        }

        private (string, ResourceStatus) GetStatus(string phase, ContainerStatus[] initContainerStatuses,
            ContainerStatus[] containerStatuses)
        {
            switch (phase)
            {
                case "Pending":
                    if (!initContainerStatuses.Any() && !containerStatuses.Any())
                    {
                        return ("Pending", ResourceStatus.InProgress);
                    }
                    return initContainerStatuses.All(HasCompleted) 
                        ? GetStatus(containerStatuses) 
                        : GetInitializingStatus(initContainerStatuses);
                case "Failed":
                    return (GetReason(containerStatuses.FirstOrDefault()), ResourceStatus.Failed);
                case "Succeeded":
                    return (GetReason(containerStatuses.FirstOrDefault()), ResourceStatus.Successful);
                default:
                    return ("Running", ResourceStatus.Successful);
            }
        }

        private static (string, ResourceStatus) GetInitializingStatus(ContainerStatus[] initContainerStatuses)
        {
            var erroredContainer = initContainerStatuses.FirstOrDefault(HasError);
            if (erroredContainer != null)
            {
                return ($"Init:{GetReason(erroredContainer)}", ResourceStatus.Failed);
            }

            var totalInit = initContainerStatuses.Length;
            var readyInit = initContainerStatuses.Where(HasCompleted).Count();
            return ($"Init:{readyInit}/{totalInit}", ResourceStatus.InProgress);
        }

        private static (string, ResourceStatus) GetStatus(ContainerStatus[] containerStatuses)
        {
            var erroredContainer = containerStatuses.FirstOrDefault(HasError);
            if (erroredContainer != null)
            {
                return (GetReason(erroredContainer), ResourceStatus.Failed);
            }
            return (GetReason(containerStatuses.FirstOrDefault(HasReason)), ResourceStatus.InProgress);
        }
        
        private static string GetReason(ContainerStatus status)
        {
            if (status == null)
            {
                return null;
            }
            
            if (status.State.Terminated != null)
            {
                return status.State.Terminated.Reason;
            }

            if (status.State.Waiting != null)
            {
                return status.State.Waiting.Reason;
            }

            return null;
        }

        private static bool HasError(ContainerStatus status)
        {
            if (status.State.Terminated != null)
            {
                return status.State.Terminated.Reason != "Completed";
            }

            if (status.State.Waiting != null)
            {
                return status.State.Waiting.Reason != "PodInitializing" &&
                       status.State.Waiting.Reason != "ContainerCreating";
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