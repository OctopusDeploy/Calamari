using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageUsages : List<UsageDetails>
    {
        public CacheAge GetCacheAgeAtFirstPackageUse()
        {
            return Count > 0
                ? this.Min(m => m.CacheAgeAtUsage)
                : new CacheAge(int.MaxValue);
        }
    }
}