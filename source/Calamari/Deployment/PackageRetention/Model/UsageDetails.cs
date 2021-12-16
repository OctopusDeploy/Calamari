using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class UsageDetails : IUsageDetails
    {
        public CacheAge CacheAgeAtUsage { get; }
        public ServerTaskId DeploymentTaskId { get; }

        [JsonConstructor]
        public UsageDetails(ServerTaskId deploymentTaskId, CacheAge cacheAgeAtUsage)
        {
            CacheAgeAtUsage = cacheAgeAtUsage;
            DeploymentTaskId = deploymentTaskId;
        }
    }
}