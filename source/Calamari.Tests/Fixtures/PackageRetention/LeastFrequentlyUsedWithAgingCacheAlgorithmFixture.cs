using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class LeastFrequentlyUsedWithAgingCacheAlgorithmFixture
    {
        [Test]
        public void WhenSpaceNeededIsGreaterThanSpaceUsed_ThenThrowException()
        {
            var lfu = new LeastFrequentlyUsedWithAgingCacheAlgorithm();
            Assert.Throws<InsufficientCacheSpaceException>(() => lfu.GetPackagesToRemove(new List<JournalEntry>(), 10_000));
        }

        [Test]
        public void WhenPackageIsLocked_ThenDoNotConsiderItForRemoval()
        {
            var lfu = new LeastFrequentlyUsedWithAgingCacheAlgorithm();

            //If this entry wasn't locked, we would expect it to be removed
            var lockedEntry = CreateEntry("package-locked", "1.0", 20, ("task-1", 1));
            lockedEntry.AddLock(new ServerTaskId("task-0"), new CacheAge(1));

            //This entry would not be removed if the locked entry wasn't locked, because it has multiple, more recent usages.
            var unlockedEntry = CreateEntry("package-unlocked", "1.0", 20, ("task-2",12), ("task-3",15));

            var entries = new List<JournalEntry>(new[] { lockedEntry, unlockedEntry });
            var packagesToRemove = lfu.GetPackagesToRemove(entries, 20);

            packagesToRemove.Should().BeEquivalentTo(CreatePackageIdentity("package-unlocked", "1.0"));
        }

        [Test]
        public void WhenUsingAllThreeFactors_ThenReturnThePackageWithTheLowestValue()
        {
            var spaceNeeded = 10;

            var entries = new[]
            {
                CreateEntry("package-1", "1.0", 10, ("task-1", 2), ("task-2", 3)),
                CreateEntry("package-2", "1.1", 10, ("task-3", 1)),
                CreateEntry("package-3", "1.0", 10, ("task-4", 2)), //Lower value - has a newer version, and is only used once.
                CreateEntry("package-3", "1.1", 10, ("task-5", 3))
            };

            var packagesToRemove = new LeastFrequentlyUsedWithAgingCacheAlgorithm(0.5M, 1, 1)
                                   .GetPackagesToRemove(entries, spaceNeeded)
                                   .OrderBy(p => p.PackageId.Value)
                                   .ThenBy(p => p.Version)
                                   .ToList();

            packagesToRemove.Should().BeEquivalentTo(CreatePackageIdentity("package-3", "1.0"));
        }

        [TestCaseSource(nameof(ExpectMultiplePackageIdsTestCaseSource))]
        public void ExpectingMultiplePackages(JournalEntry[] entries, int spaceNeeded, PackageIdentity[] expectedPackageIdVersionPairs)
        {
            var packagesToRemove = new LeastFrequentlyUsedWithAgingCacheAlgorithm()
                                   .GetPackagesToRemove(entries, spaceNeeded)
                                   .OrderBy(p => p.PackageId.Value)
                                   .ThenBy(p => p.Version)
                                   .ToList();

            packagesToRemove.Should()
                            .SatisfyRespectively(expectedPackageIdVersionPairs
                                                 .Select<PackageIdentity, Action<PackageIdentity>>
                                                     (p =>
                                                          o => o.Should().BeEquivalentTo(p))
                                                 .ToArray());
        }

        public static IEnumerable ExpectMultiplePackageIdsTestCaseSource()
        {
            yield return SetUpTestCase("WhenOnlyRelyingOnAge_ReturnEnoughPackagesToFreeEnoughSpace",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 5, ("task-1", 1)),
                                           CreateEntry("package-2", "1.0", 5, ("task-2", 10)),
                                           CreateEntry("package-3", "1.0", 5, ("task-3", 11))
                                       },
                                       10,
                                       CreatePackageIdentity("package-1", "1.0"),
                                       CreatePackageIdentity("package-2", "1.0"));

            yield return SetUpTestCase("WhenOnlyRelyingOnNewerVersions_ThenReturnPackageWithFewestNewVersions",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1)),
                                           CreateEntry("package-1", "1.1", 10, ("task-2", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-3", 1))
                                       },
                                       10,
                                       CreatePackageIdentity("package-1", "1.0"));

            yield return SetUpTestCase("WhenOnlyRelyingOnAge_ThenReturnOldestThatTakesEnoughSpace",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-2", 10))
                                       },
                                       10,
                                       CreatePackageIdentity("package-1", "1.0"));

            yield return SetUpTestCase("WhenOnlyRelyingOnHitCount_ThenReturnPackageWithFewestHits",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1), ("task-3", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-2", 1))
                                       },
                                       10,
                                       CreatePackageIdentity("package-2", "1.0"));

            yield return SetUpTestCase("WhenOnlyRelyingOnNewerVersions_ThenReturnPackagesWithMostNewVersions",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10),
                                           CreateEntry("package-1", "1.1", 10),
                                           CreateEntry("package-1", "1.2", 10),
                                           CreateEntry("package-1", "1.3", 10),
                                           CreateEntry("package-2", "1.0", 10),
                                           CreateEntry("package-2", "1.1", 10),
                                           CreateEntry("package-2", "1.2", 10)
                                       },
                                       30,
                                       CreatePackageIdentity("package-1", "1.0"),
                                       CreatePackageIdentity("package-1", "1.1"),
                                       CreatePackageIdentity("package-2", "1.0"));
        }

        static TestCaseData SetUpTestCase(string testName, JournalEntry[] testJournalEntries, int spaceNeeded, params PackageIdentity[] expectedPackageIdentities)
        {
            return new TestCaseData(testJournalEntries, spaceNeeded, expectedPackageIdentities).SetName(testName);
        }

        static JournalEntry CreateEntry(string packageId,
                                        string version,
                                        long packageSize,
                                        params (string serverTaskId, int cacheAgeAtUsage)[] usages)
        {
            if (usages.Length == 0)
                usages = new[] { ("Task-1", 1) };

            var packageUsages = new PackageUsages();
            packageUsages.AddRange(usages.Select(details => new UsageDetails(new ServerTaskId(details.serverTaskId), new CacheAge(details.cacheAgeAtUsage))));
            var packageIdentity = CreatePackageIdentity(packageId, version);

            return new JournalEntry(packageIdentity, packageSize, null, packageUsages);
        }
        
        static PackageIdentity CreatePackageIdentity(string packageId, string packageVersion)
        {
            var version = VersionFactory.CreateSemanticVersion(packageVersion);
            return new PackageIdentity(new PackageId(packageId), version, new PackagePath($"C:\\{packageId}.{packageVersion}.zip"));
        }
    }
}