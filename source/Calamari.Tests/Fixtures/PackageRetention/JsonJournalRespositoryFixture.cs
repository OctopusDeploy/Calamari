using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JsonJournalRepositoryFixture
    {
        [SetUp]
        public void SetUp()
        {

        }

        [TearDown]
        public void TearDown()
        {
            
        }

        [Test]
        public void WhenAJournalEntryIsCommittedAndRetrieved_ThenItShouldBeEquivalentToTheOriginal()
        {
            var thePackage = new PackageIdentity("TestPackage", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);
            var cacheAge = new CacheAge(10);
            var serverTaskId = new ServerTaskId("TaskID-1");
            journalEntry.AddLock(serverTaskId, cacheAge);
            journalEntry.AddUsage(serverTaskId, cacheAge);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>());

            var repository = repositoryFactory.CreateJournalRepository();
            repository.AddJournalEntry(journalEntry);
            repository.Commit();

            var updated = repositoryFactory.CreateJournalRepository();
            updated.TryGetJournalEntry(thePackage, out var retrieved).Should().BeTrue();

            retrieved.Package.Should().BeEquivalentTo(journalEntry.Package);
            retrieved.GetLockDetails().Should().BeEquivalentTo(journalEntry.GetLockDetails());
            retrieved.GetUsageDetails().Should().BeEquivalentTo(journalEntry.GetUsageDetails());
        }
    }
}