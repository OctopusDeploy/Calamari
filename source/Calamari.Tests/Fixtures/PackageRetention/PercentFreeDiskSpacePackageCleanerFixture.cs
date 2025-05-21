using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.FeatureToggles;
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
    public class PercentFreeDiskSpacePackageCleanerFixture
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
        [TestCase(null, 0)]
        [TestCase("0", 0)]
        [TestCase("4", 6)]
        [TestCase("100", 0)]
        public void WhenQuantityToKeepIsConfigured(string quantityToKeep, int expectedQuantityToRemove)
        {
            var variables = new CalamariVariables();
            variables.Add("MachinePackageCacheRetentionQuantityToKeep", quantityToKeep);
            variables.Set(KnownVariables.EnabledFeatureToggles, "configurable-package-cache-retention");
            
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PercentFreeDiskSpacePackageCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(expectedQuantityToRemove);
        }

        [Test]
        public void WhenThereIsEnoughFreeSpace_NothingIsRemoved()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(1000, 1000);
            var log = new InMemoryLog();
            var subject = new PercentFreeDiskSpacePackageCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Should().BeEmpty();
        }

        [Test]
        public void WhenFreeingUpAPackageWorthOfSpace_OnePackageIsMarkedForRemoval()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(500, 5000);
            var log = new InMemoryLog();
            var subject = new PercentFreeDiskSpacePackageCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(1);
        }

        [Test]
        public void WhenFreeingUpTwoPackagesWorthOfSpace_TwoPackagesAreMarkedForRemoval()
        {
            var variables = new CalamariVariables();
            var fileSystem = new FileSystemThatHasSpace(1000, 10000);
            var log = new InMemoryLog();
            var subject = new PercentFreeDiskSpacePackageCleaner(fileSystem, new FirstInFirstOutJournalEntrySort(), variables, log);
            var result = subject.GetPackagesToRemove(MakeSomeJournalEntries());
            result.Count().Should().Be(2);
        }

        static IEnumerable<JournalEntry> MakeSomeJournalEntries()
        {
            for (var i = 0; i < 10; i++)
            {
                var packageIdentity = new PackageIdentity(new PackageId("HelloWorld"), new SemanticVersion(1, 0, i), new PackagePath($"C:\\{i}.zip"));
                yield return new JournalEntry(packageIdentity, 1000);
            }
        }
    }
}