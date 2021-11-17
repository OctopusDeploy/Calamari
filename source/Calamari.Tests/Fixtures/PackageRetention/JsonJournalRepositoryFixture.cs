using System;
using System.IO;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
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

        [TestCase("PackageRetentionJournal.json")]
        [TestCase(null)]
        public void WhenTheJournalIsLoadedAndCommittedTo_ThenTheJournalContainsTheCorrectContents(string packageRetentionJournalPath)
        {
            var journalPath = packageRetentionJournalPath == null ? null : Path.Combine(testDir, packageRetentionJournalPath);

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns(journalPath);
            variables.Get(TentacleVariables.Agent.TentacleHome).Returns(testDir);

            var thePackage = new PackageIdentity("TestPackage", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables);

            var repository = repositoryFactory.CreateJournalRepository();
            repository.AddJournalEntry(journalEntry);
            repository.Commit();

            var updated = repositoryFactory.CreateJournalRepository();
            updated.TryGetJournalEntry(thePackage, out var journalEntryFromFile).Should().BeTrue();
            journalEntryFromFile.Package.Should().BeEquivalentTo(thePackage);
        }
    }
} 