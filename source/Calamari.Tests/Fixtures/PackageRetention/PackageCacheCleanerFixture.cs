using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class PackageCacheCleanerFixture
    {
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TestEnvironment.GetTestPath());
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        public void WhenQuantityToKeepIsConfigured_RemoveExcessPackages()
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityToKeep", "2");
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");

            var journalEntry1 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-3), "Package1");
            var journalEntry2 = MakeAJournalEntry(1, DateTime.Now.AddMonths(-1), "Package2");
            var journalEntry3 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-2), "Package3");
            var journalEntry4 = MakeAJournalEntry(4, DateTime.Now.AddDays(-1), "Package4");

            var expectedResult = new List<PackageIdentity> { journalEntry1.Package, journalEntry3.Package };
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(new[] { journalEntry1, journalEntry2, journalEntry3, journalEntry4 });
            result.Should().BeEquivalentTo(expectedResult);
        }

        [Test]
        [TestCase(null, 0)]
        [TestCase("0", 0)]
        [TestCase("100", 0)]
        public void WhenQuantityToKeepIsConfigured_KeepRequiredPackages(string quantityToKeep, int expectedQuantityToRemove)
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityToKeep", quantityToKeep);
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");

            var journalEntry1 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-3), "Package1");
            var journalEntry2 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-1), "Package2");
            var journalEntry3 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-2), "Package3");
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(new[] { journalEntry1, journalEntry2, journalEntry3 });
            result.Count().Should().Be(expectedQuantityToRemove);
        }
        
        [Test]
        public void WhenQuantityToKeepIsConfigured_AndNoPackagesExist_RemoveNoPackages()
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityToKeep", "5");
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(Array.Empty<JournalEntry>());
            result.Should().BeEmpty();
        }

        [Test]
        public void WhenThereIsEnoughFreeSpace_NothingIsRemoved()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(1000, 1000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Should().BeEmpty();
        }

        [Test]
        public void WhenFreeingUpAPackageWorthOfSpace_OnePackageIsMarkedForRemoval()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(1);
        }

        [Test]
        public void WhenFreeingUpTwoPackagesWorthOfSpace_TwoPackagesAreMarkedForRemoval()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(1000, 10000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(2);
        }

        static IEnumerable<JournalEntry> MakeSomeJournalEntries()
        {
            for (var i = 0; i < 10; i++)
            {
                var packageIdentity = new PackageIdentity(new PackageId("HelloWorld"), new SemanticVersion(1, 0, i), new PackagePath($"C:\\{i}.zip"));
                var packageUsages = new PackageUsages { new UsageDetails(new ServerTaskId($"Deployments-{i}"), new CacheAge(i), DateTime.Now.AddDays(i)) };
                yield return new JournalEntry(packageIdentity, 1000, packageUsages: packageUsages);
            }
        }

        static JournalEntry MakeAJournalEntry(int numberOfUsages, DateTime mostRecentUsage, string packageName)
        {
            var packageUsages = new PackageUsages();
            for (var i = 0; i < numberOfUsages; i++)
            {
                packageUsages.Add(new UsageDetailsBuilder().WithDeploymentTaskId(new ServerTaskId($"Deployments-{i}")).WithCacheAgeAtUsage(new CacheAge(i)).WithDateTime(mostRecentUsage.AddDays(0 - i)).Build());
            }
            
            return new JournalEntryBuilder()
                               .WithPackageIdentity(new PackageIdentityBuilder()
                                                    .WithPackageId(new PackageId(packageName))
                                                    .WithVersion(new SemanticVersion(1, 0, 0))
                                                    .WithPath(new PackagePath($"C:\\{packageName}.zip"))
                                                    .Build())
                               .WithPackageUsages(packageUsages)
                               .WithFileSizeBytes(1000)
                               .Build();
        }
    }
}