using System;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IUsageDetails
    {
        CacheAge CacheAge { get; }
        DateTime DateTime { get; }
        ServerTaskId DeploymentTaskId { get; }
    }
}