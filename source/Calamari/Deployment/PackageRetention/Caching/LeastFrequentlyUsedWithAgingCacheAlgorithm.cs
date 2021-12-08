using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
namespace Calamari.Deployment.PackageRetention.Caching
{
    public class LeastFrequentlyUsedWithAgingCacheAlgorithm : RetentionAlgorithmBase
    {
        readonly decimal ageFactor;
        readonly decimal hitFactor;
        readonly decimal newerVersionFactor;

        public LeastFrequentlyUsedWithAgingCacheAlgorithm(decimal ageFactor = 0.5m, decimal hitFactor = 1m, decimal newerVersionFactor = 1m)
        {
            this.ageFactor = ageFactor;
            this.hitFactor = hitFactor;
            this.newerVersionFactor = newerVersionFactor;
        }

        public override IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceRequired)
        {
            journalEntries = journalEntries.Where(e => !e.HasLock());

            //We don't need the actual age of cache entries, just the relative age.
            var currentCacheAge = journalEntries.Max(je => je.GetUsageDetails().Max(u => u.CacheAgeAtUsage));
            var orderedPackages = OrderByValue(journalEntries, currentCacheAge);

            var spaceFound = 0L;

            var packagesToRemove = orderedPackages.TakeWhile(p =>
                                                             {
                                                                 if (spaceFound >= spaceRequired) return false; //Already found enough space

                                                                 spaceFound += p.Package.FileSizeBytes;
                                                                 return true;
                                                             }).ToList();

            if (spaceFound >= spaceRequired)
                return packagesToRemove.Select(pi => pi.Package);

            if (spaceFound == 0)
                throw new InsufficientCacheSpaceException(spaceFound, spaceRequired, $"No space was available to be freed.");

            throw new InsufficientCacheSpaceException(spaceFound, spaceRequired);
        }

        IEnumerable<PackageInfo> OrderByValue(IEnumerable<JournalEntry> entries, CacheAge currentCacheAge)
        {
            if (!entries.Any()) return new PackageInfo[0];

            var details = entries.Select(e => new PackageInfo(e, entries)).ToList();

            (int Min, int Max) newVersionCountRange = (details.Min(d => d.NewerVersionCount), details.Max(d => d.NewerVersionCount));
            //Note that the age max/min seem backwards, but that's because the larger value is more recent (we record the age of the *cache* at the time of use).
            (int Min, int Max) ageRange = (currentCacheAge.Value - details.Max(d => d.Age.Value), currentCacheAge.Value - details.Min(d => d.Age.Value));
            (int Min, int Max) hitCountRange = (details.Min(d => d.HitCount), details.Max(d => d.HitCount));

            decimal NormaliseVersionCount(int newerVersionCount) => Normalise(newerVersionCount, newVersionCountRange.Min, newVersionCountRange.Max);
            decimal NormaliseAge(int age) => Normalise(age, ageRange.Min, ageRange.Max);
            decimal NormaliseHitCount(int hitCount) => Normalise(hitCount, hitCountRange.Min, hitCountRange.Max);

            return details.OrderBy(d =>
                                       NormaliseHitCount(d.HitCount) * hitFactor
                                        - NormaliseAge(currentCacheAge.Value - d.Age.Value) * ageFactor
                                        - NormaliseVersionCount(d.NewerVersionCount) * newerVersionFactor);
        }

        decimal Normalise(int value, int rangeMinValue, int rangeMaxValue)
        {
            var divisor = rangeMaxValue - rangeMinValue;
            divisor = divisor == 0 ? 1 : divisor;
            var scale = 1M / divisor; //Scales from 0..1
            return (value - rangeMinValue) * scale;
        }



        class PackageInfo
        {
            public PackageIdentity Package { get; set; }

            public CacheAge Age { get; }
            public int HitCount { get; }
            public int NewerVersionCount { get; }

            public PackageInfo(JournalEntry entry, IEnumerable<JournalEntry> entries)
            {
                Package = entry.Package;
                Age = GetAge(entry);
                HitCount = GetHitCount(entry);
                NewerVersionCount = GetNumberOfNewerVersions(entry, entries);
            }

            CacheAge GetAge(JournalEntry entry)
            {
                return entry.GetUsageDetails().Min(ud => ud.CacheAgeAtUsage);
            }

            int GetHitCount(JournalEntry entry)
            {
                return entry.GetUsageDetails().Count();
            }

            int GetNumberOfNewerVersions(JournalEntry entry, IEnumerable<JournalEntry> entries)
            {
                return entries.Count(e => e.Package.PackageId == entry.Package.PackageId && e.Package.Version.CompareTo(entry.Package.Version) > 0);
            }
        }
    }
}