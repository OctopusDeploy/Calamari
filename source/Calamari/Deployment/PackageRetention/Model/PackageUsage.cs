using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageUsage
    {
        [JsonProperty]
        readonly List<UsageDetails> usages;

        [JsonConstructor]
        internal PackageUsage(List<UsageDetails> usages = null)
        {
            this.usages = usages ?? new List<UsageDetails>();
        }

        public void AddUsage(ServerTaskId deploymentTaskId, int cacheAge)
        {
            usages.Add(new UsageDetails(deploymentTaskId, cacheAge));
        }

        public IEnumerable<UsageDetails> GetUsageDetails()
        {
            return usages;
        }

        public int GetUsageCount()
        {
            return GetUsageDetails().Count();
        }
    }
}