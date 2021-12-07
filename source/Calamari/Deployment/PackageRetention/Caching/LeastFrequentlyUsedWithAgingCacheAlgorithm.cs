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
        const decimal AgeFactor = 0.5m;
        const decimal HitFactor = 1m;
        const decimal NewerVersionFactor = 0.5m;

        public override IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceNeeded)
        {
            journalEntries = journalEntries.Where(e => !e.HasLock());

            //We don't need the actual age of cache entries, just the relative age.
            var currentCacheAge = journalEntries.Max(je => je.GetUsageDetails().Max(u => u.CacheAgeAtUsage));
            var orderedPackages = OrderByValue(journalEntries, currentCacheAge);

            var spaceFound = 0L;

            var packagesToRemove = orderedPackages.TakeWhile(p =>
                                                             {
                                                                 spaceFound += p.PackageIdentity.FileSizeBytes;
                                                                 return spaceFound < spaceNeeded;
                                                             });

            if (spaceFound < spaceNeeded)
            {
                if (spaceFound == 0)
                    throw new InsufficientCacheSpaceException($"No space was available to be freed.");

                throw new InsufficientCacheSpaceException($"Could only free { BytesToString(spaceFound)} for the required {BytesToString(spaceNeeded)}.");
            }

            return packagesToRemove.Select(pi => pi.PackageIdentity);
        }

        //From https://stackoverflow.com/a/4975942
        static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        IEnumerable<PackageInfo> OrderByValue(IEnumerable<JournalEntry> entries, CacheAge currentCacheAge)
        {
            if (!entries.Any()) return new PackageInfo[0];

            var details = entries.Select(e => new PackageInfo(e, entries)).ToList();

            var maxNumberOfNewerVersions = details.Max(d => d.NewerVersionCount);
            var minNumberOfNewerVersions = details.Min(d => d.NewerVersionCount);

            Func<int, int> scaleVersionCount = (int newerVersionCount) => ScaleRange(newerVersionCount, minNumberOfNewerVersions, maxNumberOfNewerVersions);

            return details.OrderBy(d => d.HitCount * HitFactor - (currentCacheAge.Value - d.Age.Value) * AgeFactor - scaleVersionCount(d.NewerVersionCount) * NewerVersionFactor);
        }

        int ScaleRange(int newerVersionCount, int minNumberOfNewerVersions, int maxNumberOfNewerVersions)
        {
            var scale = 1 / (maxNumberOfNewerVersions - minNumberOfNewerVersions); //Scales from 0..1
            return (newerVersionCount - minNumberOfNewerVersions) * scale;
        }

        class PackageInfo
        {
            public PackageIdentity PackageIdentity { get; set; }

            public CacheAge Age { get; }
            public int HitCount { get; }
            public int NewerVersionCount { get; }

            public PackageInfo(JournalEntry entry, IEnumerable<JournalEntry> entries)
            {
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