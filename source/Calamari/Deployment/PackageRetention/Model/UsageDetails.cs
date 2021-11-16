using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class UsageDetails : IUsageDetails
    {
        public int CacheAge { get; }
        public DateTime DateTime { get; }
        public ServerTaskId DeploymentTaskId { get; }

        /// <summary> Defaults DateTime to DateTime.Now </summary>
        public UsageDetails(ServerTaskId deploymentTaskId, int cacheAge)
            : this(deploymentTaskId, cacheAge, DateTime.Now)
        {
        }

        public UsageDetails(ServerTaskId deploymentTaskId, int cacheAge, DateTime dateTime)
        {
            CacheAge = cacheAge;
            DateTime = dateTime;
            DeploymentTaskId = deploymentTaskId;
        }
    }
}