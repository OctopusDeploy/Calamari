using System;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IUsageDetails
    {
        CacheAge CacheAgeAtUsage { get; }
        DateTime DateTime { get; }
        ServerTaskId DeploymentTaskId { get; }
    }
}