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
        public void WhenOldestEntryHasEnoughSpaceToUse_ThenReturnIt()
        {
            var entries = new List<JournalEntry>
            {
                CreateEntry("package-1", "1.0.0", 10_000, new []{("task-1", 1)}),
                CreateEntry("package-2", "1.0.0", 10_000, new []{("task-2", 10)})
            };

            var lfu = new LeastFrequentlyUsedWithAgingCacheAlgorithm();

            lfu.GetPackagesToRemove(entries, 10_000).Should().ContainSingle().Which.PackageId.Value.Should().Be("package-1");
        }


        JournalEntry CreateEntry(string packageId,
                                 string version,
                                 long packageSize,
                                 params (string serverTaskId, int age)[] usages)
        {
            var packageUsages = new PackageUsages();
            packageUsages.AddRange(usages.Select(details => new UsageDetails(new ServerTaskId(details.serverTaskId), new CacheAge(details.age))));

            return new JournalEntry(new PackageIdentity(packageId, version, packageSize), null, packageUsages);
        }
    }
}