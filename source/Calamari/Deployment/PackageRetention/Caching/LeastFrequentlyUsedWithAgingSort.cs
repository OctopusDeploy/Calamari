using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class LeastFrequentlyUsedWithAgingSort : ISortJournalEntries
    {
        readonly decimal ageFactor;
        readonly decimal hitFactor;
        readonly decimal newerVersionFactor;

        public LeastFrequentlyUsedWithAgingSort(decimal ageFactor = 0.5m, decimal hitFactor = 1m, decimal newerVersionFactor = 1m)
        {
            this.ageFactor = ageFactor;
            this.hitFactor = hitFactor;
            this.newerVersionFactor = newerVersionFactor;
        }

        public IEnumerable<JournalEntry> Sort(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();

            //We don't need the actual age of cache entries, just the relative age.
            var currentCacheAge = entries.Max(je => je.GetUsageDetails().Max(u => u.CacheAgeAtUsage));

            return OrderByValue(entries.ToList(), currentCacheAge).Select(v => v.Entry);
        }

        IEnumerable<LeastFrequentlyUsedJournalEntry> OrderByValue(IList<JournalEntry> journalEntries, CacheAge currentCacheAge)
        {
            if (!journalEntries.Any()) return new LeastFrequentlyUsedJournalEntry[0];
            var details = CreateLeastFrequentlyUsedJournalEntries(journalEntries).ToList();

            var hitCountRange = details.GetHitCountRange();
            var cacheAgeRange = details.GetCacheAgeRange(currentCacheAge);
            var newVersionCountRange = details.GetNewVersionCountRange();

            decimal CalculateValue(LeastFrequentlyUsedJournalEntry pi) =>
                Normalise(pi.HitCount, hitCountRange) * hitFactor
                - Normalise(currentCacheAge.Value - pi.Age.Value, cacheAgeRange) * ageFactor
                - Normalise(pi.NewerVersionCount, newVersionCountRange) * newerVersionFactor;

            return details.OrderBy(CalculateValue);
        }

        static IEnumerable<LeastFrequentlyUsedJournalEntry> CreateLeastFrequentlyUsedJournalEntries(IList<JournalEntry> journalEntries)
        {
            var packageIdVersions = journalEntries
                                    .GroupBy(entry => entry.Package.PackageId)
                                    .ToDictionary(entries => entries.Key, entries => entries.OrderByDescending(e => e.Package.Version).ToList());

            foreach (var kvp in packageIdVersions)
            {
                var current = 0;
                foreach (var entry in kvp.Value)
                {
                    current++;
                    var age = entry.GetUsageDetails().Min(ud => ud.CacheAgeAtUsage);
                    var hitCount = entry.GetUsageDetails().Count();
                    yield return new LeastFrequentlyUsedJournalEntry(entry, age, hitCount, current);
                }
            }
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