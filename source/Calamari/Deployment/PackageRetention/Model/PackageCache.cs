using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageCache
    {
        [JsonProperty]
        public CacheAge CacheAge { get; private set; }

        public PackageCache(int cacheAge)
        {
            CacheAge = new CacheAge(cacheAge);
        }

        [JsonConstructor]
        public PackageCache(CacheAge cacheAge)
        {
            CacheAge = cacheAge;
        }

        public void IncrementCacheAge()
        {
            CacheAge.IncrementAge();
        }
    }
}