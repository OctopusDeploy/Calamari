using System;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
//using Octopus.Diagnostics;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalEntryFixture
    {
        IVariables variables;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            variables.Set(KnownVariables.Calamari.EnablePackageRetention, bool.TrueString);
            variables.Set(TentacleVariables.Agent.TentacleHome, "SomeDirectory");
        }

        [Test]
        public void WhenPackageUsageIsRegistered_ThenALockExists()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());

            journal.RegisterPackageUse(thePackage, theDeployment);

            Assert.IsTrue(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageUsageIsDeregistered_ThenNoLocksExist()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());

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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
            journal.RegisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenTwoPackagesAreRegisteredAgainstTheSameDeployment_ThenTwoSeparateUsagesAreRecorded()
        {
            var package1 = new PackageIdentity("Package1", "1.0");
            var package2 = new PackageIdentity("Package2", "1.0");
            var theDeployment = new ServerTaskId("Deployment-1");

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);

            Assert.AreEqual(2, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenPackageIsRegisteredAndDeregistered_ThenUsageIsStillRecorded()
        {
            var thePackage = new PackageIdentity("Package", "1.0");
            var deploymentOne = new ServerTaskId("Deployment-1");

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<ILog>());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.DeregisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
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

            var journal = new Journal(new InMemoryJournalRepositoryFactory(), ourVariables, Substitute.For<ILog>());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            var didRegisterPackage = journal.GetUsageCount(thePackage) == 1;
            Assert.That(didRegisterPackage == shouldBeEnabled);
        }
    }
}