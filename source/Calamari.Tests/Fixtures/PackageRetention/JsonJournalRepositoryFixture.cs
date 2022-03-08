using System;
using System.ComponentModel;
using System.IO;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JsonJournalRepositoryFixture
    {
        readonly string testDir = TestEnvironment.GetTestPath("Fixtures", "JsonJournalRepository");

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(testDir))
                Directory.CreateDirectory(testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }

        static PackageIdentity CreatePackageIdentity(string packageId, string packageVersion)
        {
            var version = VersionFactory.CreateSemanticVersion(packageVersion);
            return new PackageIdentity(new PackageId(packageId), version, new PackagePath($"C:\\{packageId}.{packageVersion}.zip"));
        }

        [Test]
        public void WhenAJournalEntryIsCommittedAndRetrieved_ThenItShouldBeEquivalentToTheOriginal()
        {
            var journalPath = Path.Combine(testDir, "PackageRetentionJournal.json");

            var thePackage = CreatePackageIdentity("TestPackage", "0.0.1");
            var cacheAge = new CacheAge(10);
            var serverTaskId = new ServerTaskId("TaskID-1");

            var journalEntry = new JournalEntry(thePackage, 1);
            journalEntry.AddLock(serverTaskId, cacheAge);
            journalEntry.AddUsage(serverTaskId, cacheAge);

            var writeRepository = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), new StaticJsonJournalPathProvider(journalPath), Substitute.For<ILog>());
            writeRepository.AddJournalEntry(journalEntry);
            writeRepository.Commit();

            var readRepository = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), new StaticJsonJournalPathProvider(journalPath), Substitute.For<ILog>());
            readRepository.Load();
            readRepository.TryGetJournalEntry(thePackage, out var retrieved).Should().BeTrue();

            retrieved.Package.Should().BeEquivalentTo(journalEntry.Package);
            retrieved.GetLockDetails().Should().BeEquivalentTo(journalEntry.GetLockDetails());
            retrieved.GetUsageDetails().Should().BeEquivalentTo(journalEntry.GetUsageDetails());
        }

        [Test]
        public void WhenThereIsAnErrorReadingTheJournal_ThenTheJournalIsRenamed()
        {
            const string invalidJson = @"[{""a"",}]";
            var journalPath = Path.Combine(testDir, "PackageRetentionJournal.json");

            File.WriteAllText(journalPath, invalidJson);

            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.PackageRetentionJournalPath, journalPath);

            var journal = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), new StaticJsonJournalPathProvider(journalPath), Substitute.For<ILog>());
            journal.Load();

            Directory.GetFiles(testDir, "PackageRetentionJournal_*.json").Length.Should().Be(1);
        }
    }
}