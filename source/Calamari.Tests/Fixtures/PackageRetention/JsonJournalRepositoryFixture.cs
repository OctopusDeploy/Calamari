using System;
using System.IO;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "JsonJournalRepository");

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(TentacleHome))
                Directory.CreateDirectory(TentacleHome);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TentacleHome))
                Directory.Delete(TentacleHome, true);
        }

        [Test]
        public void WhenCalamariPackageRetentionJournalPathExists_ThenTheJournalIsCreatedAtTheGivenPath()
        {
            var journalPath = Path.Combine(TentacleHome, "PackageRetentionJournal.json");

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns(journalPath);

            var repository = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables, Substitute.For<ILog>());

            repository.Commit();
            Assert.IsTrue(File.Exists(journalPath));
        }

        [Test]
        public void WhenCalamariPackageRetentionJournalPathDoesNotExist_ThenTheJournalIsCreatedAtTheDefaultPath()
        {
            var homeDir = TentacleHome;

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns((string) null);
            variables.Get(TentacleVariables.Agent.TentacleHome).Returns(homeDir);

            var repository = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables, Substitute.For<ILog>());

            var expectedPath = Path.Combine(homeDir, JsonJournalRepository.DefaultJournalName);

            repository.Commit();
            Assert.IsTrue(File.Exists(expectedPath));
        }

        [Test]
        public void WhenThereIsAnErrorReadingTheJournal_ThenTheJournalIsRenamed()
        {
            const string invalidJson = @"[{""a"",}]";
            var journalPath = Path.Combine(TentacleHome, "PackageRetentionJournal.json");

            File.WriteAllText(journalPath, invalidJson);

            var variables = Substitute.For<IVariables>();
            variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns(journalPath);

            var repository = new JsonJournalRepository(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>(), variables, Substitute.For<ILog>());

            Directory.GetFiles(TentacleHome, "PackageRetentionJournal_*.json").Length.Should().Be(1);
        }
    }
}