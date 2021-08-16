using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageUsage
    {
        readonly Dictionary<DeploymentID, List<DateTime>> usages;

        internal PackageUsage(Dictionary<DeploymentID, List<DateTime>> usageRecord = null)
        {
            usages = usageRecord ?? new Dictionary<DeploymentID, List<DateTime>>();
        }

        public void AddUsage(DeploymentID deploymentID)
        {
            if (!usages.ContainsKey(deploymentID))
                usages.Add(deploymentID, new List<DateTime>());

            usages[deploymentID].Add(DateTime.Now);
        }

        public IEnumerable<DateTime> GetUsageDetails()
        {
            return usages.SelectMany(u => u.Value);
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