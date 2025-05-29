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
    public class PackageQuantityPackageCacheCleanerFixture
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
        public void WhenFindingPackagesToRemove_WithQuantityOfPackagesAndQuantityOfVersionsSet_ReturnExcessPackages()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "2" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", "2" },
                { "MachinePackageCacheRetentionStrategy", "Quantities" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
        
            var package1Version1 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-1), "Package1", 1);
            var package1Version2 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-5), "Package1", 2);
            var package1Version3 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-7), "Package1", 3);
            
            var package2Version1 = MakeAJournalEntry(1, DateTime.Now.AddMonths(-4), "Package2", 1);
            
            var package3Version1 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-3), "Package3", 1);
            var package3Version2 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-6), "Package3", 2);
            
            var package4Version1 = MakeAJournalEntry(4, DateTime.Now.AddDays(-1), "Package4", 1);

            var existingJournals = new[] { package4Version1 , package2Version1, package3Version2, package1Version3, package3Version1, package1Version2, package1Version1 };
            var expectedResult = new List<PackageIdentity> { package3Version1.Package, package3Version2.Package, package2Version1.Package, package1Version3.Package };
            
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(existingJournals);
            result.Should().BeEquivalentTo(expectedResult);
        }
        
        [Test]
        public void WhenFindingPackagesToRemove__WithKeepAllPackagesAndQuantityOfVersionsSet__ReturnExcessPackageVersions()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "-1" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", "2" },
                { "MachinePackageCacheRetentionStrategy", "Quantities" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
        
            var package1Version1 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-1), "Package1", 1);
            var package1Version2 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-5), "Package1", 2);
            var package1Version3 = MakeAJournalEntry(2, DateTime.Now.AddMonths(-7), "Package1", 3);
            
            var package2Version1 = MakeAJournalEntry(1, DateTime.Now.AddMonths(-4), "Package2", 1);
            
            var package3Version1 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-3), "Package3", 1);
            var package3Version2 = MakeAJournalEntry(3, DateTime.Now.AddMonths(-6), "Package3", 2);
            
            var package4Version1 = MakeAJournalEntry(4, DateTime.Now.AddDays(-1), "Package4", 1);

            var existingJournals = new[] { package4Version1 , package2Version1, package3Version2, package1Version3, package3Version1, package1Version2, package1Version1 };
            var expectedResult = new List<PackageIdentity> { package1Version3.Package };
            
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(existingJournals);
            result.Should().BeEquivalentTo(expectedResult);
        }
        
        [Test]
        [TestCase("100", "100")]
        public void WhenFindingPackagesToRemove_WhenQuantityToKeepIsHigh_ReturnNoPackages(string quantityOfPackagesToKeep, string quantityOfVersionsToKeep)
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", quantityOfPackagesToKeep },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", quantityOfVersionsToKeep },
                { "MachinePackageCacheRetentionStrategy", "Quantities" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
        
            var package1Version1 = MakeAJournalEntry(3, DateTime.Now, "Package1", 1);
            var package1Version2 = MakeAJournalEntry(3, DateTime.Now, "Package1", 2);
            var package1Version3 = MakeAJournalEntry(3, DateTime.Now, "Package1", 3);
            var package2Version1 = MakeAJournalEntry(3, DateTime.Now, "Package2", 1);
            var package3Version1 = MakeAJournalEntry(3, DateTime.Now, "Package3", 1);
            
            var existingJournals = new[] { package1Version1, package1Version2, package1Version3, package2Version1, package3Version1 };
        
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(existingJournals);
            result.Should().BeEmpty();
        }
        
        [Test]
        public void WhenFindingPackagesToRemove_AndNoPackagesExist_ReturnNoPackages()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "5" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", "5" },
                { "MachinePackageCacheRetentionStrategy", "Quantities" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
            
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(Array.Empty<JournalEntry>());
            result.Should().BeEmpty();
        }
        
        [Test]
        public void WhenFindingPackagesToRemove_WhenQuantityOfVersionsToKeepIsNotSet_KeepDefaultVersionCount()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "10" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", null },
                { "MachinePackageCacheRetentionStrategy", "Quantities" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
        
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(5);
        }
        
        [Test]
        public void WhenMachinePackageCacheStrategyIsNotQuantity_ReturnNoPackages()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "100" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", "100" },
                { "MachinePackageCacheRetentionStrategy", "FreeSpace" },
                { KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention" }
            };
            
            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Should().BeEmpty();
        }
        
        [Test]
        public void WhenConfigurablePackageCacheRetentionToggleIsDisabled_ReturnNoPackages()
        {
            var variables = new CalamariVariables
            {
                { "MachinePackageCacheRetentionQuantityOfPackagesToKeep", "100" },
                { "MachinePackageCacheRetentionQuantityOfVersionsToKeep", "100" },
                { "MachinePackageCacheRetentionStrategy", "Quantities" }
            };

            var log = new InMemoryLog();
            var subject = new PackageQuantityPackageCacheCleaner(variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Should().BeEmpty();
        }

        static IEnumerable<JournalEntry> MakeSomeJournalEntries()
        {
            for (var i = 0; i < 10; i++)
            {
                var packageIdentity = new PackageIdentityBuilder()
                                      .WithPackageId(new PackageId("HelloWorld"))
                                      .WithVersion(new SemanticVersion(1, 0, i))
                                      .WithPath(new PackagePath($"C:\\{i}.zip"))
                                      .Build();
                var packageUsages = new PackageUsages { new UsageDetailsBuilder().WithDeploymentTaskId(new ServerTaskId($"Deployments-{i}")).WithCacheAgeAtUsage(new CacheAge(i)).WithDateTime(DateTime.Now.AddDays(i)).Build() };
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