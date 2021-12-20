using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
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

        Journal journal;
        IVariables variables;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);
            variables.Set(TentacleVariables.Agent.TentacleHome, "SomeDirectory");

            journal = new Journal(
                               new InMemoryJournalRepositoryFactory(),
                               Substitute.For<ILog>(),
                               Substitute.For<ICalamariFileSystem>(),
                               Substitute.For<IRetentionAlgorithm>(),
                               variables,
                               Substitute.For<IFreeSpaceChecker>()
                              );
        }

        [Test]
        public void WhenPackageUsageIsRegistered_ThenALockExists()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            journal.RegisterPackageUse(thePackage, theDeployment);

            Assert.IsTrue(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageUsageIsDeregistered_ThenNoLocksExist()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            journal.RegisterPackageUse(thePackage, theDeployment);
            journal.DeregisterPackageUse(thePackage, theDeployment);

            Assert.IsFalse(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForOne_ThenALockExists()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);
            journal.DeregisterPackageUse(thePackage, deploymentOne);

            Assert.IsTrue(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForBoth_ThenNoLocksExist()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);
            journal.DeregisterPackageUse(thePackage, deploymentOne);
            journal.DeregisterPackageUse(thePackage, deploymentTwo);

            Assert.IsFalse(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegistered_ThenUsageIsRecorded()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            journal.RegisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenTwoPackagesAreRegisteredAgainstTheSameDeployment_ThenTwoSeparateUsagesAreRecorded()
        {
            var package1 = new PackageIdentity("Package1", "1.0");
            var package2 = new PackageIdentity("Package2", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            journal.RegisterPackageUse(package1, theDeployment);
            journal.RegisterPackageUse(package2, theDeployment);

            Assert.AreEqual(1, journal.GetUsage(package1).Count());
            Assert.AreEqual(1, journal.GetUsage(package2).Count());
        }

        [Test]
        public void WhenOnePackageIsRegisteredForTwoDeployments_ThenTwoSeparateUsagesAreRecorded()
        {
            var thePackage = new PackageIdentity("Package1", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");
            var deploymentTwo = new ServerTaskId("Deployment-2");

            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);

            Assert.AreEqual(2, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenPackageIsRegisteredAndDeregistered_ThenUsageIsStillRecorded()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.DeregisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
        }

        [Test]
<<<<<<< HEAD:source/Calamari.Tests/Fixtures/PackageRetention/JournalFixture.cs
        public void WhenRetentionIsApplied_ThenPackageFileAndUsageAreRemoved()
        {
            var packageOnePath = "./PackageOne.zip";
            var packageOne = new PackageIdentity("PackageOne", "1.0", 1000, VersionFormat.Semver, packageOnePath);

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>(){ packageOne });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(packageOnePath).Returns(true);

            var thisJournal = new Journal(new InMemoryJournalRepositoryFactory(), Substitute.For<ILog>(), fileSystem, retentionAlgorithm, variables, Substitute.For<IFreeSpaceChecker>());

            thisJournal.RegisterPackageUse(packageOne, new ServerTaskId("Deployment-1"));
            thisJournal.ApplyRetention(PackageDirectory);

            thisJournal.GetUsage(packageOne).Should().BeEmpty();
            fileSystem.Received().DeleteFile(packageOne.Path, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenRetentionIsAppliedAndCacheSpaceIsNotSufficient_ThenPackageFileAndUsageAreRemoved()
        {
            var existingPackagePath = "./PackageOne.zip";
            var existingPackage = new PackageIdentity("PackageOne", "1.0", 1 * 1024 * 1024, VersionFormat.Semver, existingPackagePath);  //Package is 1 MB

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>(){ existingPackage });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(existingPackagePath).Returns(true);
            fileSystem.GetDiskFreeSpace(Arg.Any<string>(), out _)
                      .Returns(x => {
                                   x[1] = 10000000000000;//lots of free disk space
                                   return true;
                               });

            variables.Add(Journal.PackageRetentionCacheSizeInMegaBytesVariable, "1"); //Cache size is 1MB

            var thisJournal = new Journal(new InMemoryJournalRepositoryFactory(), Substitute.For<ILog>(), fileSystem, retentionAlgorithm, variables, Substitute.For<IFreeSpaceChecker>());

            thisJournal.RegisterPackageUse(existingPackage, new ServerTaskId("Deployment-1"));
            thisJournal.ApplyRetention(PackageDirectory);

            thisJournal.GetUsage(existingPackage).Should().BeEmpty();
            fileSystem.Received().DeleteFile(existingPackage.Path, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenRetentionIsAppliedAndCacheSpaceIsSufficientButDiskSpaceIsNot_ThenPackageFileAndUsageAreRemoved()
        {
            var existingPackagePath = "./PackageOne.zip";
            var existingPackage = new PackageIdentity("PackageOne", "1.0", 1 * 1024 * 1024, VersionFormat.Semver, existingPackagePath);  //Package is 1 MB

            var retentionAlgorithm = Substitute.For<IRetentionAlgorithm>();
            retentionAlgorithm.GetPackagesToRemove(Arg.Any<IEnumerable<JournalEntry>>(), Arg.Any<long>()).Returns(new List<PackageIdentity>(){ existingPackage });

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(existingPackagePath).Returns(true);
            fileSystem.GetDiskFreeSpace(Arg.Any<string>(), out _)
                      .Returns(x => {
                                   x[1] = 0.5M;// 0.5MB free
                                   return true;
                               });

            variables.Add(Journal.PackageRetentionCacheSizeInMegaBytesVariable, "10"); //Cache size is 10MB

            var thisJournal = new Journal(new InMemoryJournalRepositoryFactory(), Substitute.For<ILog>(), fileSystem, retentionAlgorithm, variables, Substitute.For<IFreeSpaceChecker>());

            thisJournal.RegisterPackageUse(existingPackage, new ServerTaskId("Deployment-1"));
            thisJournal.ApplyRetention(PackageDirectory);

            thisJournal.GetUsage(existingPackage).Should().BeEmpty();
            fileSystem.Received().DeleteFile(existingPackage.Path, FailureOptions.IgnoreFailure);
=======
        public void WhenStaleLocksAreExpired_TheLocksAreRemoved()
        {
            var thePackage = new PackageIdentity("Package", "1.0");

            var packageLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-1"), new CacheAge(1), new DateTime(2021, 1, 1))
            };

            var journalEntry = new JournalEntry(thePackage, packageLocks);

            var journalEntries = new Dictionary<PackageIdentity, JournalEntry>()
            {
                { thePackage, journalEntry }
            };
            
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);

            var testJournal = new Journal(new InMemoryJournalRepositoryFactory(journalEntries), variables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());

            testJournal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(testJournal.HasLock(thePackage));
        }

        [Test]
        public void OnlyStaleLocksAreExpired()
        {
            var packageOne = new PackageIdentity("PackageOne", "1.0");
            var packageTwo = new PackageIdentity("PackageTwo", "1.0");
            
            var packageOneLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-1"), new CacheAge(1), new DateTime(2021, 1, 1)),
            };
            
            var packageTwoLocks = new PackageLocks
            {
                new UsageDetails(new ServerTaskId("Deployment-2"), new CacheAge(1), DateTime.Now),
            };
            
            var packageOneJournalEntry = new JournalEntry(packageOne, packageOneLocks);
            var packageTwoJournalEntry = new JournalEntry(packageTwo, packageTwoLocks);

            var journalEntries = new Dictionary<PackageIdentity, JournalEntry>()
            {
                { packageOne, packageOneJournalEntry },
                { packageTwo, packageTwoJournalEntry }
            };
            
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);

            var testJournal = new Journal(new InMemoryJournalRepositoryFactory(journalEntries), variables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());

            testJournal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(testJournal.HasLock(packageOne));
            Assert.IsTrue(testJournal.HasLock(packageTwo));
>>>>>>> master:source/Calamari.Tests/Fixtures/PackageRetention/JournalEntryFixture.cs
        }

        [TestCase(true, true, true, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, true, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, false, false, false)]
        public void WhenRetentionIsDisabled_DoNotAllowPackageUsageToBeRecorded(bool setRetentionEnabledFromServer, bool setPackageRetentionJournalPath, bool setTentacleHome, bool shouldBeEnabled)
        {
            //Retention should only be enabled if EnablePackageRetention is true, and either of TentacleHome and PackageRetentionJournalPath are set.
            var ourVariables = new CalamariVariables();

            if (setRetentionEnabledFromServer)
                ourVariables.Set(KnownVariables.Calamari.EnablePackageRetention, Boolean.TrueString);

            if (setTentacleHome)
                ourVariables.Set(TentacleVariables.Agent.TentacleHome, "TentacleHome");

            if (setPackageRetentionJournalPath)
                ourVariables.Set(KnownVariables.Calamari.PackageRetentionJournalPath, "JournalPath");

            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            var thisJournal = new Journal(new InMemoryJournalRepositoryFactory(),Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), Substitute.For<IRetentionAlgorithm>(), ourVariables, Substitute.For<IFreeSpaceChecker>());
            thisJournal.RegisterPackageUse(thePackage, deploymentOne);
            var didRegisterPackage = thisJournal.GetUsageCount(thePackage) == 1;
            Assert.That(didRegisterPackage == shouldBeEnabled);
        }
    }
}