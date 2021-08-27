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
        readonly Dictionary<ServerTaskID, List<DateTime>> usages;

        [JsonConstructor]
        internal PackageUsage(Dictionary<ServerTaskID, List<DateTime>> usages = null)
        {
            this.usages = usages ?? new Dictionary<ServerTaskID, List<DateTime>>();
        }

        public void AddUsage(ServerTaskID deploymentID)
        {
            if (!usages.ContainsKey(deploymentID))
                usages.Add(deploymentID, new List<DateTime>());

            usages[deploymentID].Add(DateTime.Now);
        }

        public IEnumerable<DateTime> GetUsageDetails()
        {
            return usages.SelectMany(u => u.Value);
        }

        public Dictionary<ServerTaskID, List<DateTime>> AsDictionary()
        {
            return usages;
        }
        
        /*
        public IEnumerable<(DateTime When, int Count)> GetUsageCountsWhens()
        {
            return usages.SelectMany(u => u.Value)
                         .GroupBy(i => i)
                         .Select(group => (When: group.Key, Count: group.Count()));
        }      */
    }
}