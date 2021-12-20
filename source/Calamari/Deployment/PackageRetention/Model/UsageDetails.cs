using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class UsageDetails : IUsageDetails
    {
        public CacheAge CacheAgeAtUsage { get; }
        public DateTime DateTime { get; }
        public ServerTaskId DeploymentTaskId { get; }
        
        /// <summary> Defaults DateTime to DateTime.Now </summary>
        public UsageDetails(ServerTaskId deploymentTaskId, CacheAge cacheAge)
            : this(deploymentTaskId, cacheAge, DateTime.Now)
        {
        }

        [JsonConstructor]
        public UsageDetails(ServerTaskId deploymentTaskId, CacheAge cacheAgeAtUsage, DateTime dateTime)
        {
            CacheAgeAtUsage = cacheAgeAtUsage;
            DateTime = dateTime;
            DeploymentTaskId = deploymentTaskId;
        }
    }
}