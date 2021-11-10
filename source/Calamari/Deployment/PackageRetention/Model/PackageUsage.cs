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
        readonly Dictionary<ServerTaskId, List<DateTime>> usages;

        [JsonConstructor]
        internal PackageUsage(Dictionary<ServerTaskId, List<DateTime>> usages = null)
        {
            this.usages = usages ?? new Dictionary<ServerTaskId, List<DateTime>>();
        }

        public void AddUsage(ServerTaskId deploymentTaskId)
        {
            if (!usages.ContainsKey(deploymentTaskId))
                usages.Add(deploymentTaskId, new List<DateTime>());

            usages[deploymentTaskId].Add(DateTime.Now);
        }

        public IEnumerable<DateTime> GetUsageDetails()
        {
            return usages.SelectMany(u => u.Value);
        }

        public Dictionary<ServerTaskId, List<DateTime>> AsDictionary()
        {
            return usages;
        }
    }
}