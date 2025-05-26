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
            variables.Add("MachinePackageCacheRetentionQuantityOfPackagesToKeep", "2");
            variables.Add("MachinePackageCacheRetentionQuantityOfVersionsToKeep", "2");
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");
        
            var package1Version1 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-1), "Package1", 1);
            var package1Version2 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-2), "Package1", 2);
            var package1Version3 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-3), "Package1", 3);
            
            var package2Version1 = MakeAJournalEntry(1, DateTime.Now.AddMonths(-4), "Package2", 1);
            
            var package3Version1 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-5), "Package3", 1);
            var package3Version2 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-6), "Package3", 2);
            
            var package4Version1 = MakeAJournalEntry(4, DateTime.Now.AddDays(-1), "Package4", 1);

            var existingJournals = new[] { package1Version1, package1Version2, package1Version3, package2Version1, package3Version1, package3Version2, package4Version1 };
            var expectedResult = new List<PackageIdentity> { package3Version1.Package, package3Version2.Package, package2Version1.Package, package1Version3.Package };
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(existingJournals);
            result.Should().BeEquivalentTo(expectedResult);
        }
        
        [Test]
        [TestCase("100", "100")]
        public void WhenQuantityToKeepIsHigh_KeepAllPackages(string quantityOfPackagesToKeep, string quantityOfVersionsToKeep)
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityOfPackagesToKeep", quantityOfPackagesToKeep);
            variables.Add("MachinePackageCacheRetentionQuantityOfVersionsToKeep", quantityOfVersionsToKeep);
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");
        
            var package1Version1 = MakeAJournalEntry(3, DateTime.Now, "Package1", 1);
            var package1Version2 = MakeAJournalEntry(3, DateTime.Now, "Package1", 2);
            var package1Version3 = MakeAJournalEntry(3, DateTime.Now, "Package1", 3);
            var package2Version1 = MakeAJournalEntry(3, DateTime.Now, "Package2", 1);
            var package3Version1 = MakeAJournalEntry(3, DateTime.Now, "Package3", 1);
            
            var existingJournals = new[] { package1Version1, package1Version2, package1Version3, package2Version1, package3Version1 };

            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(existingJournals);
            result.Should().BeEmpty();
        }
        
        [Test]
        public void WhenQuantityToKeepIsConfigured_AndNoPackagesExist_RemoveNoPackages()
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityOfPackagesToKeep", "5");
            variables.Add("MachinePackageCacheRetentionQuantityOfVersionsToKeep", "5");
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(Array.Empty<JournalEntry>());
            result.Should().BeEmpty();
        }
        
        [Test]
        [TestCase("0")]
        [TestCase(null)]
        public void WhenQuantityOfVersionsToKeepIsNotSet_FindPackagesToRemoveByPercentFreeDiskSpace(string quantityOfVersionsToKeep)
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityOfPackagesToKeep", "10");
            variables.Add("MachinePackageCacheRetentionQuantityOfVersionsToKeep", quantityOfVersionsToKeep);
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");

            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(1);
        }
        
        [Test]
        public void WhenConfigurablePackageCacheRetentionToggleIsDisabled_FindPackagesToRemoveByPercentFreeDiskSpace()
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityOfPackagesToKeep", "100");
            variables.Add("MachinePackageCacheRetentionQuantityOfVersionsToKeep", "100");

            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PackageCacheCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(1);
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

        static JournalEntry MakeAJournalEntry(int numberOfUsages, DateTime mostRecentUsage, string packageName, int version)
        {
            var packageUsages = new PackageUsages();
            for (var i = 0; i < numberOfUsages; i++)
            {
                packageUsages.Add(new UsageDetailsBuilder().WithDeploymentTaskId(new ServerTaskId($"Deployments-{i}")).WithCacheAgeAtUsage(new CacheAge(i)).WithDateTime(mostRecentUsage.AddDays(0 - i)).Build());
            }
            
            return new JournalEntryBuilder()
                               .WithPackageIdentity(new PackageIdentityBuilder()
                                                    .WithPackageId(new PackageId(packageName))
                                                    .WithVersion(new SemanticVersion(version, 0, 0))
                                                    .WithPath(new PackagePath($"C:\\{packageName}.zip"))
                                                    .Build())
                               .WithPackageUsages(packageUsages)
                               .WithFileSizeBytes(1000)
                               .Build();
        }
    }
}