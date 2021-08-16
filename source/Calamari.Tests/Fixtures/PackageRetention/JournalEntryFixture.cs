using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalEntryFixture
    {
        [Test]
        public void WhenPackageUsageIsRegistered_ThenALockExists()
        {
            var thePackage = new PackageID("Package");
            var theDeployment = new DeploymentID("Deployment-1");
            var journal = new Journal(new JournalInMemoryRepositoryFactory());

            journal.RegisterPackageUse(thePackage, theDeployment);

            Assert.IsTrue(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageUsageIsDeregistered_ThenNoLocksExist()
        {
            var thePackage = new PackageID("Package");
            var theDeployment = new DeploymentID("Deployment-1");
            var journal = new Journal(new JournalInMemoryRepositoryFactory());

            journal.RegisterPackageUse(thePackage, theDeployment);
            journal.DeregisterPackageUse(thePackage, theDeployment);

            Assert.IsFalse(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForOne_ThenALockExists()
        {
            var thePackage = new PackageID("Package");
            var deploymentOne = new DeploymentID("Deployment-1");
            var deploymentTwo = new DeploymentID("Deployment-2");

            var journal = new Journal(new JournalInMemoryRepositoryFactory());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);
            journal.DeregisterPackageUse(thePackage, deploymentOne);

            Assert.IsTrue(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegisteredForTwoDeploymentsAndDeregisteredForBoth_ThenNoLocksExist()
        {
            var thePackage = new PackageID("Package");
            var deploymentOne = new DeploymentID("Deployment-1");
            var deploymentTwo = new DeploymentID("Deployment-2");

            var journal = new Journal(new JournalInMemoryRepositoryFactory());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);
            journal.DeregisterPackageUse(thePackage, deploymentOne);
            journal.DeregisterPackageUse(thePackage, deploymentTwo);

            Assert.IsFalse(journal.HasLock(thePackage));
        }

        [Test]
        public void WhenPackageIsRegistered_ThenUsageIsRecorded()
        {
            var thePackage = new PackageID("Package");
            var deploymentOne = new DeploymentID("Deployment-1");

            var journal = new Journal(new JournalInMemoryRepositoryFactory());
            journal.RegisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenTwoPackagesAreRegistered_ThenTwoUsagesAreRecorded()
        {
            var thePackage = new PackageID("Package");
            var deploymentOne = new DeploymentID("Deployment-1");
            var deploymentTwo = new DeploymentID("Deployment-1");

            var journal = new Journal(new JournalInMemoryRepositoryFactory());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.RegisterPackageUse(thePackage, deploymentTwo);

            Assert.AreEqual(2, journal.GetUsage(thePackage).Count());
        }

        [Test]
        public void WhenPackageIsRegisteredAndDeregistered_ThenUsageIsStillRecorded()
        {

            var thePackage = new PackageID("Package");
            var deploymentOne = new DeploymentID("Deployment-1");

            var journal = new Journal(new JournalInMemoryRepositoryFactory());
            journal.RegisterPackageUse(thePackage, deploymentOne);
            journal.DeregisterPackageUse(thePackage, deploymentOne);

            Assert.AreEqual(1, journal.GetUsage(thePackage).Count());
        }
    }
}