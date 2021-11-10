using Calamari.Common.Plumbing.Deployment.PackageRetention;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class PackageIdentityFixture
    {
        [Test]
        public void WhenTwoPackagesWithTheSameNameAndVersionAreCompared_ThenTheyAreEqual()
        {
            var packageA = new PackageIdentity("Package1", "1.0");
            var package1 = new PackageIdentity("Package1", "1.0");

            Assert.AreEqual(package1,packageA);
        }

        [Test]
        public void WhenTwoPackagesWithTheSameNameAndDifferentVersionAreCompared_ThenTheyAreNotEqual()
        {
            var package1v1 = new PackageIdentity("Package1", "1.0");
            var package1v2 = new PackageIdentity("Package1", "2.0");

            Assert.AreNotEqual(package1v1,package1v2);
        }
        [Test]
        public void WhenTwoPackagesWithADifferentNameAndSameVersionAreCompared_ThenTheyAreNotEqual()
        {
            var package1 = new PackageIdentity("Package1", "1.0");
            var package2 = new PackageIdentity("Package2", "1.0");

            Assert.AreNotEqual(package1,package2);
        }
    }
}