using System.IO;
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
        [Test]
        public void MakeSureMyCodeActuallyWorks()
        {
            var thePackage = new PackageIdentity("TestPackage", "0.0.1");
            var journalEntry = new JournalEntry(thePackage);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), Substitute.For<ISemaphoreFactory>());

            var repository = repositoryFactory.CreateJournalRepository();
            repository.AddJournalEntry(journalEntry);
            repository.Commit();

            var updated = repositoryFactory.CreateJournalRepository();
            updated.TryGetJournalEntry(thePackage, out var journalEntryFromFile).Should().BeTrue();
            journalEntryFromFile.Package.Should().BeEquivalentTo(thePackage);
        }
    }
}