using System;
using System.Diagnostics;
using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class LeastFrequentlyUsedWithAgingSortPerformanceFixture
    {
        [Test]
        public void Performs()
        {
            var journalRepo = SeedJournal();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var algo = new LeastFrequentlyUsedWithAgingSort();
            algo
                .Sort(journalRepo.GetAllJournalEntries())
                .ToList();
            stopWatch.Stop();
            stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        }

        static InMemoryJournalRepository SeedJournal()
        {
            var journalRepo = new InMemoryJournalRepository();
            var journal = new PackageJournal(journalRepo,
                                             Substitute.For<ILog>(),
                                             new TestCalamariPhysicalFileSystem(),
                                             Substitute.For<IRetentionAlgorithm>(),
                                             new SystemSemaphoreManager());
            var serverTask = new ServerTaskId("ServerTasks-1");
            for (var i = 0; i < 50000; i++)
            {
                var package = new PackageIdentity(new PackageId($"Package-{i % 100}"), new SemanticVersion(1, 0, i), new PackagePath($"C:\\{i}"));
                journal.RegisterPackageUse(package, serverTask, 100);
            }

            journal.RemoveAllLocks(serverTask);
            return journalRepo;
        }
    }
}