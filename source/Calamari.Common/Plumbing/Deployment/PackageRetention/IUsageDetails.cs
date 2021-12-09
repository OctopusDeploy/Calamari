using System;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IUsageDetails
    {
        CacheAge CacheAgeAtUsage { get; }
        ServerTaskId DeploymentTaskId { get; }
    }
}