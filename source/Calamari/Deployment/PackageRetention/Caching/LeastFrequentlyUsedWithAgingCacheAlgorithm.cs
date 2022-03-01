using System;
using System.Collections.Generic;
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
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();

            //We don't need the actual age of cache entries, just the relative age.
            var currentCacheAge = entries.Max(je => je.GetUsageDetails().Max(u => u.CacheAgeAtUsage));
            var orderedPackages = OrderByValue(entries.ToList(), currentCacheAge);

            var spaceFound = 0L;

            var packagesToRemove = orderedPackages.TakeWhile(p =>
                                                             {
                                                                 if (spaceFound >= spaceRequired)
                                                                     return false; //Already found enough space

                                                                 spaceFound += p.FileSizeBytes;
                                                                 return true;
                                                             }).ToList();
            if (spaceFound == 0)
                throw new InsufficientCacheSpaceException(spaceFound, spaceRequired, $"No space was available to be freed.");

            return packagesToRemove.Select(pi => pi.Package);
        }

        IEnumerable<PackageInfo> OrderByValue(IList<JournalEntry> journalEntries, CacheAge currentCacheAge)
        {
            if (!journalEntries.Any()) return new PackageInfo[0];

            var details = journalEntries.Select(e => new PackageInfo(e, journalEntries)).ToList();

            decimal CalculateValue(PackageInfo pi) =>
                Normalise(pi.HitCount, details.GetHitCountRange()) * hitFactor
                - Normalise(currentCacheAge.Value - pi.Age.Value, details.GetCacheAgeRange(currentCacheAge)) * ageFactor
                - Normalise(pi.NewerVersionCount, details.GetNewVersionCountRange()) * newerVersionFactor;

            return details.OrderBy((Func<PackageInfo, decimal>)CalculateValue);
        }

        static decimal Normalise(int value, (int Min, int Max) range)
        {
            var divisor = range.Max - range.Min;
            divisor = divisor == 0 ? 1 : divisor;
            var scale = 1M / divisor; //Scales from 0..1
            return (value - range.Min) * scale;
        }
    }
}