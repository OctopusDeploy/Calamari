using System;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IUsageDetails
    {
        int CacheAge { get; }
        DateTime DateTime { get; }
        ServerTaskId DeploymentTaskId { get; }
    }
}