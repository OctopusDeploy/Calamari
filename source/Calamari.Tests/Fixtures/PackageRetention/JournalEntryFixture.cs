using Calamari.Deployment.PackageRetention;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalEntryFixture
    {
        [Test]
        public void WhenALockIsAdded_ThenALockExists()
        {
            var thePackage = "ThePackage";
            var entry = new JournalEntry(thePackage);
            entry.AddLock("Deployment-1");

            Assert.IsTrue(entry.HasLock());
        }

        [Test]
        public void WhenSingleExistingLockIsRemoved_ThenNoLocksExist()
        {
            var thePackage = "ThePackage";
            var deploymentID = "Deployment-1";
            var entry = new JournalEntry(thePackage);
            entry.AddLock(deploymentID);
            entry.RemoveLock(deploymentID);

            Assert.IsFalse(entry.HasLock());
        }

        [Test]
        public void WhenMultipleLocksExistForSinglePackageAndOneLockIsRemoved_ThenLocksExist()
        {
            var thePackage = "ThePackage";
            var deploymentOne = "Deployment-1";
            var deploymentTwo = "Deployment-2";
            var entry = new JournalEntry(thePackage);
            entry.AddLock(deploymentOne);
            entry.AddLock(deploymentTwo);
            entry.RemoveLock(deploymentOne);

            Assert.IsTrue(entry.HasLock());
        }

        [Test]
        public void WhenMultipleLocksExistForSinglePackageAndTheLocksAreRemoved_ThenNoLocksExist()
        {
            var thePackage = "ThePackage";
            var deploymentOne = "Deployment-1";
            var deploymentTwo = "Deployment-2";
            var entry = new JournalEntry(thePackage);
            entry.AddLock(deploymentOne);
            entry.AddLock(deploymentTwo);
            entry.RemoveLock(deploymentOne);
            entry.RemoveLock(deploymentTwo);

            Assert.IsFalse(entry.HasLock());
        }
    }
}