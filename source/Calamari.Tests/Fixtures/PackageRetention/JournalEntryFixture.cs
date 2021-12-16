using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalEntryFixture
    {
        Journal journal;

        [SetUp]
        public void Setup()
        {
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);
            variables.Set(TentacleVariables.Agent.TentacleHome, "SomeDirectory");
            variables.Set(KnownVariables.Calamari.PackageRetentionJournalPath, "JournalPath");
        }

        [Test]
        public void WhenTestSetupCompletes_ThenPackageRetentionIsEnabled()
        {
            variables.IsPackageRetentionEnabled().Should().BeTrue("We need retention to be enabled for these tests to be valid.");
            journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());;
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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(journalEntries), variables, Substitute.For<ILog>());

            journal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(journal.HasLock(thePackage));
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
            
            var journal = new Journal(new InMemoryJournalRepositoryFactory(journalEntries), variables, Substitute.For<ILog>());

            journal.ExpireStaleLocks(TimeSpan.FromDays(14));

            Assert.IsFalse(journal.HasLock(packageOne));
            Assert.IsTrue(journal.HasLock(packageTwo));
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

            var myJournal = new Journal(new InMemoryJournalRepositoryFactory(), ourVariables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());
            myJournal.RegisterPackageUse(thePackage, deploymentOne);
            var didRegisterPackage = myJournal.GetUsageCount(thePackage) == 1;
            Assert.That(didRegisterPackage == shouldBeEnabled);
        }
    }
}