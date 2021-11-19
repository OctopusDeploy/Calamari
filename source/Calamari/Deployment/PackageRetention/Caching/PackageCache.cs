using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Caching
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
    /*
    public class PackageCache
    {
        public PackageCache()
        {

        }

        public void Ensure(int spaceInMB)
        {

        }

        //To determine package value, we need:
        /*
         *  - Age
         *  - Hits
         *  - Count of newer versions
         *  - Size(?)
        
    }      */
                           /*
    public class CacheEntry
    {
        public PackageIdentity Package { get; }

        public int Age { get; set; }
        public int Hits { get; set; }
        public int NewerVersionCount { get; set; }
        public decimal SizeInMB { get; set; } = 0;

        public CacheEntry(JournalEntry journalEntry)
        {
            Identity = journalEntry.Package;
        }

        public decimal GetPackageValue()
        {
               
        }
    }      */
}