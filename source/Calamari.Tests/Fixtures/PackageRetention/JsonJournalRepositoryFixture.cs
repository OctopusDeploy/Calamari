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
        readonly string tentacleHome = TestEnvironment.GetTestPath("Fixtures", "JsonJournalRepository");

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(tentacleHome))
                Directory.CreateDirectory(tentacleHome);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tentacleHome))
                Directory.Delete(tentacleHome, true);
        }

        [Test]
        public void WhenCalamariPackageRetentionJournalPathExists_ThenTheJournalIsCreatedAtTheGivenPath()
        {
            var journalPath = Path.Combine(tentacleHome, "PackageRetentionJournal.json");
            var thePackage = new PackageIdentity("TestPackageWithJournalPath", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns(journalPath);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables);

            var repository = repositoryFactory.CreateJournalRepository();
            repository.AddJournalEntry(journalEntry);
            repository.Commit();

            var updated = repositoryFactory.CreateJournalRepository();
            updated.TryGetJournalEntry(thePackage, out var journalEntryFromFile).Should().BeTrue();
            journalEntryFromFile.Package.Should().BeEquivalentTo(thePackage);
        }

        [Test]
        public void WhenCalamariPackageRetentionJournalPathDoesNotExist_ThenTheJournalIsCreatedAtTheDefaultPath()
        {
            var thePackage = new PackageIdentity("TestPackageWithTentacleHome", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns((string) null);
            variables.Get(TentacleVariables.Agent.TentacleHome).Returns(tentacleHome);

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