using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using FluentAssertions;
using NUnit.Framework;

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
        public void WhenUsingAllThreeFactors_ThenReturnThePackageOneWithTheLowestValue()
        {
            var spaceNeeded = 10;

            var entries = new[]
            {
                CreateEntry("package-1", "1.0", 10, ("task-1", 2), ("task-2", 3)),
                CreateEntry("package-2", "1.1", 10, ("task-3", 1)),
                CreateEntry("package-3", "1.0", 10, ("task-4", 2)),
                CreateEntry("package-3", "1.1", 10, ("task-5", 3))
            };

            var packagesToRemove = new LeastFrequentlyUsedWithAgingCacheAlgorithm(0.5M, 1, 1)
                                   .GetPackagesToRemove(entries, spaceNeeded)
                                   .OrderBy(p => p.PackageId.Value)
                                   .ThenBy(p => p.Version)
                                   .ToList();

            packagesToRemove.Should()
                            .ContainSingle();

            var package = packagesToRemove.FirstOrDefault();
            package.PackageId.Value.Should().BeEquivalentTo("package-3");
            package.Version.ToString().Should().BeEquivalentTo("1.0");
        }

        [TestCaseSource(nameof(ExpectMultiplePackageIdsTestCaseSource))]
        public void ExpectingMultiplePackages(JournalEntry[] entries, int spaceNeeded, ExpectedPackageIdAndVersion[] expectedPackageIdVersionPairs)
        {
            var packagesToRemove = new LeastFrequentlyUsedWithAgingCacheAlgorithm()
                                   .GetPackagesToRemove(entries, spaceNeeded)
                                   .OrderBy(p => p.PackageId.Value)
                                   .ThenBy(p => p.Version)
                                   .ToList();

            packagesToRemove.Should()
                            .SatisfyRespectively(expectedPackageIdVersionPairs
                                                 .Select<ExpectedPackageIdAndVersion, Action<PackageIdentity>>(p =>
                                                                                                                   o =>
                                                                                                                   {
                                                                                                                       p.PackageId.Should().BeEquivalentTo(o.PackageId.Value);
                                                                                                                       p.Version.Should().BeEquivalentTo(o.Version.ToString());
                                                                                                                   })
                                                 .ToArray()
                                                );
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
                                       new ExpectedPackageIdAndVersion("package-1"),
                                       new ExpectedPackageIdAndVersion("package-2"));

            yield return SetUpTestCase("WhenOnlyRelyingOnNewerVersions_ThenReturnPackageWithFewestNewVersions",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1)),
                                           CreateEntry("package-1", "1.1", 10, ("task-2", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-3", 1))
                                       },
                                       10,
                                       new ExpectedPackageIdAndVersion("package-1", "1.0"));

            yield return SetUpTestCase("WhenOnlyRelyingOnAge_ThenReturnOldestThatTakesEnoughSpace",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-2", 10))
                                       },
                                       10,
                                       new ExpectedPackageIdAndVersion("package-1"));

            yield return SetUpTestCase("WhenOnlyRelyingOnHitCount_ThenReturnPackageWithFewestHits",
                                       new[]
                                       {
                                           CreateEntry("package-1", "1.0", 10, ("task-1", 1), ("task-3", 1)),
                                           CreateEntry("package-2", "1.0", 10, ("task-2", 1))
                                       },
                                       10,
                                       new ExpectedPackageIdAndVersion("package-2"));

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
                                       new ExpectedPackageIdAndVersion("package-1", "1.0"),
                                       new ExpectedPackageIdAndVersion("package-1", "1.1"),
                                       new ExpectedPackageIdAndVersion("package-2", "1.0"));
        }

        static TestCaseData SetUpTestCase(string testName, JournalEntry[] testJournalEntries, int spaceNeeded, params ExpectedPackageIdAndVersion[] expectedPackageIdAndVersions)
        {
            return new TestCaseData(testJournalEntries, spaceNeeded, expectedPackageIdAndVersions).SetName(testName);
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

            return new JournalEntry(new PackageIdentity(packageId, version, packageSize), null, packageUsages);
        }

        public class ExpectedPackageIdAndVersion
        {
            public ExpectedPackageIdAndVersion(string packageId, string version = "1.0")
            {
                PackageId = packageId;
                Version = version;
            }

            public string PackageId { get; }
            public string Version { get; }
        }
    }
}