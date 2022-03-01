using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageJournal");
        static readonly string PackageDirectory = Path.Combine(TentacleHome, "Files");

        InMemoryJournalRepository journalRepository;
        PackageJournal packageJournal;
        IVariables variables;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);
            variables.Set(TentacleVariables.Agent.TentacleHome, "SomeDirectory");
            journalRepository = new InMemoryJournalRepository();
            packageJournal = new PackageJournal(
                                                journalRepository,
                                                Substitute.For<ILog>(),
                                                Substitute.For<ICalamariFileSystem>(),
                                                Substitute.For<IRetentionAlgorithm>(),
                                                Substitute.For<IFreeSpaceChecker>(),
                                                Substitute.For<ISemaphoreFactory>()
                                               );
        }

        PackageIdentity CreatePackageIdentity(string packageId, string packageVersion)
        {
            var version = VersionFactory.CreateSemanticVersion(packageVersion);
            var path = new PackagePath($"C:\\{packageId}.{packageVersion}.zip");
            return new PackageIdentity(new PackageId(packageId), version, path);
        }

        [Test]
        public void WhenPackageUsageIsRegistered_ThenALockExists()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            packageJournal.RegisterPackageUse(thePackage, theDeployment, 1);

            Assert.IsTrue(journalRepository.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageUsageIsDeregistered_ThenNoLocksExist()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            packageJournal.RegisterPackageUse(thePackage, theDeployment, 1);
            packageJournal.RemoveAllLocks(theDeployment);

            Assert.IsFalse(journalRepository.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForOne_ThenALockExists()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            packageJournal.RegisterPackageUse(thePackage, deploymentOne, 1);
            packageJournal.RegisterPackageUse(thePackage, deploymentTwo, 1);
            packageJournal.RemoveAllLocks(deploymentOne);

            Assert.IsTrue(journalRepository.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForBoth_ThenNoLocksExist()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            packageJournal.RegisterPackageUse(thePackage, deploymentOne, 1);
            packageJournal.RegisterPackageUse(thePackage, deploymentTwo, 1);
            packageJournal.RemoveAllLocks(deploymentOne);
            packageJournal.RemoveAllLocks(deploymentTwo);

            Assert.IsFalse(journalRepository.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegistered_ThenUsageIsRecorded()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            packageJournal.RegisterPackageUse(thePackage, deploymentOne, 1);

            Assert.AreEqual(1, journalRepository.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenTwoPackagesAreRegisteredAgainstTheSameDeployment_ThenTwoSeparateUsagesAreRecorded()
        {
            var package1 = CreatePackageIdentity("Package1", "1.0");
            var package2 = CreatePackageIdentity("Package2", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            packageJournal.RegisterPackageUse(package1, theDeployment, 1);
            packageJournal.RegisterPackageUse(package2, theDeployment, 1);

            Assert.AreEqual(1, journalRepository.GetUsage(package1).Count());
            Assert.AreEqual(1, journalRepository.GetUsage(package2).Count());
        }

        [Test]
        public void WhenOnePackageIsRegisteredForTwoDeployments_ThenTwoSeparateUsagesAreRecorded()
        {
            var thePackage = CreatePackageIdentity("Package1", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            packageJournal.RegisterPackageUse(thePackage, deploymentOne, 1);
            packageJournal.RegisterPackageUse(thePackage, deploymentTwo, 1);

            Assert.AreEqual(2, journalRepository.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenPackageIsRegisteredAndDeregistered_ThenUsageIsStillRecorded()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            packageJournal.RegisterPackageUse(thePackage, deploymentOne, 1);
            packageJournal.RemoveAllLocks(deploymentOne);

            Assert.AreEqual(1, journalRepository.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenRetentionIsApplied_ThenPackageFileAndUsageAreRemoved()
        {
            var packageOne = CreatePackageIdentity("PackageOne", "1.0");

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>() { packageOne });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(packageOne.Path.Value).Returns(true);

            var thisRepository = new InMemoryJournalRepository();
            var thisJournal = new PackageJournal(thisRepository,
                                                 Substitute.For<ILog>(),
                                                 fileSystem,
                                                 retentionAlgorithm,
                                                 Substitute.For<IFreeSpaceChecker>(),
                                                 Substitute.For<ISemaphoreFactory>());

            thisJournal.RegisterPackageUse(packageOne, new ServerTaskId("Deployment-1"), 1000);
            thisJournal.ApplyRetention(PackageDirectory, 0);

            thisRepository.GetUsage(packageOne).Should().BeEmpty();
            fileSystem.Received().DeleteFile(packageOne.Path.Value, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenRetentionIsAppliedAndCacheSpaceIsNotSufficient_ThenPackageFileAndUsageAreRemoved()
        {
            var existingPackage = CreatePackageIdentity("PackageOne", "1.0");

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>() { existingPackage });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(existingPackage.Path.Value).Returns(true);
            fileSystem.GetDiskFreeSpace(Arg.Any<string>(), out _)
                      .Returns(x =>
                               {
                                   x[1] = 10000000000000; //lots of free disk space
                                   return true;
                               });

            var thisRepository = new InMemoryJournalRepository();
            var thisJournal = new PackageJournal(thisRepository,
                                                 Substitute.For<ILog>(),
                                                 fileSystem,
                                                 retentionAlgorithm,
                                                 Substitute.For<IFreeSpaceChecker>(),
                                                 Substitute.For<ISemaphoreFactory>());

            thisJournal.RegisterPackageUse(existingPackage, new ServerTaskId("Deployment-1"), 1 * 1024 * 1024); //Package is 1 MB
            thisJournal.ApplyRetention(PackageDirectory, 1);

            thisRepository.GetUsage(existingPackage).Should().BeEmpty();
            fileSystem.Received().DeleteFile(existingPackage.Path.Value, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenRetentionIsAppliedAndCacheSpaceIsSufficientButDiskSpaceIsNot_ThenPackageFileAndUsageAreRemoved()
        {
            var existingPackage = CreatePackageIdentity("PackageOne", "1.0");

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>() { existingPackage });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(existingPackage.Path.Value).Returns(true);
            fileSystem.GetDiskFreeSpace(Arg.Any<string>(), out _)
                      .Returns(x =>
                               {
                                   x[1] = 0.5M; // 0.5MB free
                                   return true;
                               });

            var thisRepository = new InMemoryJournalRepository();
            var thisJournal = new PackageJournal(thisRepository,
                                                 Substitute.For<ILog>(),
                                                 fileSystem,
                                                 retentionAlgorithm,
                                                 Substitute.For<IFreeSpaceChecker>(),
                                                 Substitute.For<ISemaphoreFactory>());

            thisJournal.RegisterPackageUse(existingPackage, new ServerTaskId("Deployment-1"), 1 * 1024 * 1024); //Package is 1 MB
            thisJournal.ApplyRetention(PackageDirectory, 10);

            thisRepository.GetUsage(existingPackage).Should().BeEmpty();
            fileSystem.Received().DeleteFile(existingPackage.Path.Value, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenStaleLocksAreExpired_TheLocksAreRemoved()
        {
            var thePackage = CreatePackageIdentity("Package", "1.0");

            var packageLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-1"), new CacheAge(1), new DateTime(2021, 1, 1))
            };

            var journalEntry = new JournalEntry(thePackage, 1, packageLocks);

            var journalEntries = new Dictionary<PackageIdentity, JournalEntry>()
            {
                { thePackage, journalEntry }
            };

            var testJournal = new PackageJournal(new InMemoryJournalRepository(journalEntries),
                                                 Substitute.For<ILog>(),
                                                 Substitute.For<ICalamariFileSystem>(),
                                                 Substitute.For<IRetentionAlgorithm>(),
                                                 Substitute.For<IFreeSpaceChecker>(),
                                                 Substitute.For<ISemaphoreFactory>());
            testJournal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(journalRepository.HasLock(thePackage));
        }

        [Test]
        public void OnlyStaleLocksAreExpired()
        {
            var packageOne = CreatePackageIdentity("PackageOne", "1.0");
            var packageTwo = CreatePackageIdentity("PackageTwo", "1.0");

            var packageOneLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-1"), new CacheAge(1), new DateTime(2021, 1, 1)),
            };

            var packageTwoLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-2"), new CacheAge(1), DateTime.Now),
            };

            var packageOneJournalEntry = new JournalEntry(packageOne, 1, packageOneLocks);
            var packageTwoJournalEntry = new JournalEntry(packageTwo, 1, packageTwoLocks);

            var journalEntries = new Dictionary<PackageIdentity, JournalEntry>()
            {
                { packageOne, packageOneJournalEntry },
                { packageTwo, packageTwoJournalEntry }
            };

            var testJournalRepository = new InMemoryJournalRepository(journalEntries);
            var testJournal = new PackageJournal(testJournalRepository,
                                                 Substitute.For<ILog>(),
                                                 Substitute.For<ICalamariFileSystem>(),
                                                 Substitute.For<IRetentionAlgorithm>(),
                                                 Substitute.For<IFreeSpaceChecker>(),
                                                 Substitute.For<ISemaphoreFactory>());
            testJournal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(testJournalRepository.HasLock(packageOne));
            Assert.IsTrue(testJournalRepository.HasLock(packageTwo));
        }
    }
}