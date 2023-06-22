#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusCheckTask
    {
        private const int PollingIntervalSeconds = 2;

        private readonly IResourceRetriever resourceRetriever;
        private readonly IResourceUpdateReporter reporter;
        private readonly IKubectl kubectl;
        private readonly Func<ITimer> timerFactory;

        public ResourceStatusCheckTask(
            IResourceRetriever resourceRetriever,
            IResourceUpdateReporter reporter,
            IKubectl kubectl,
            Func<ITimer> timerFactory)
        {
            this.resourceRetriever = resourceRetriever;
            this.reporter = reporter;
            this.kubectl = kubectl;
            this.timerFactory = timerFactory;
        }

        public async Task<Result> Run(IEnumerable<ResourceIdentifier> resources, Options options, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Unable to set KubeCtl");
            }

            var timer = timerFactory();
            timer.Duration = timeout;
            timer.Interval = TimeSpan.FromSeconds(PollingIntervalSeconds);

            var definedResources = resources.ToArray();
            var checkCount = 0;
            return await Task.Run(() =>
            {
                timer.Start();
                var result = new Result();
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return result;
                    }

                    if (!definedResources.Any())
                    {
                        return new Result(DeploymentStatus.Succeeded);
                    }

                    var definedResourceStatuses = resourceRetriever
                                                  .GetAllOwnedResources(definedResources, kubectl, options)
                                                  .ToArray();
                    var resourceStatuses = definedResourceStatuses
                                           .SelectMany(IterateResourceTree)
                                           .ToDictionary(resource => resource.Uid, resource => resource);

                    var deploymentStatus = GetDeploymentStatus(definedResourceStatuses, definedResources);

                    reporter.ReportUpdatedResources(result.ResourceStatuses, resourceStatuses, ++checkCount);

                    result = new Result(
                        deploymentStatus,
                        definedResources,
                        definedResourceStatuses,
                        resourceStatuses);

                    timer.WaitForInterval();
                } while (!timer.HasCompleted() && result.DeploymentStatus == DeploymentStatus.InProgress);

                return result;
            }, cancellationToken);
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

        public class Result
        {
            public Result() : this(DeploymentStatus.InProgress)
            {
            }

            public Result(DeploymentStatus deploymentStatus)
                : this(
                    deploymentStatus,
                    new ResourceIdentifier[0],
                    new Resource[0],
                    new Dictionary<string, Resource>())
            {
            }

            public Result(
                DeploymentStatus deploymentStatus,
                ResourceIdentifier[] definedResources,
                Resource[] definedResourceStatuses,
                Dictionary<string, Resource> resourceStatuses)
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