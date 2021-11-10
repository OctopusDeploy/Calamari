using System;
using System.IO;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

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

        [TestCase("PackageRetentionJournal.json", TestName = "CalamariPackageRetentionJournalPath is set to a non-null value")]
        [TestCase(null, TestName = "CalamariPackageRetentionJournalPath is null; default to TentacleHome")]
        public void WhenTheJournalIsLoadedAndCommittedTo_ThenTheJournalContainsTheCorrectContents(string packageRetentionJournalPath)
        {
            var journalPath = packageRetentionJournalPath == null ? null : Path.Combine(testDir, packageRetentionJournalPath);

            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.PackageRetentionJournalPath, journalPath);
            variables.Set(TentacleVariables.Agent.TentacleHome, testDir);

            var thePackage = new PackageIdentity("TestPackage", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables, Substitute.For<ILog>());

            var repository = repositoryFactory.CreateJournalRepository();
            repository.AddJournalEntry(journalEntry);
            repository.Commit();

            var updated = repositoryFactory.CreateJournalRepository();
            updated.TryGetJournalEntry(thePackage, out var journalEntryFromFile).Should().BeTrue();
            journalEntryFromFile.Package.Should().BeEquivalentTo(thePackage);
        }

        [Test]
        public void WhenThereIsAnErrorReadingTheJournal_ThenTheJournalIsRenamed()
        {
            const string invalidJson = @"[{""a"",}]";
            var journalPath = Path.Combine(testDir, "PackageRetentionJournal.json");

            File.WriteAllText(journalPath, invalidJson);

            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.PackageRetentionJournalPath, journalPath);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables, Substitute.For<ILog>());

            repositoryFactory.CreateJournalRepository();

            Directory.GetFiles(testDir, "PackageRetentionJournal_*.json").Length.Should().Be(1);
        }
    }
} 