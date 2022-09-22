using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class PackageIdentityFixture
    {
        [Test]
        public void WhenTwoPackagesWithTheSameNameAndVersionAreCompared_ThenTheyAreEqual()
        {
            var packageA = CreatePackageIdentity("Package1", "1.0");
            var package1 = CreatePackageIdentity("Package1", "1.0");

            Assert.AreEqual(package1,packageA);
        }

        [Test]
        public void WhenTwoPackagesWithTheSameNameAndDifferentVersionAreCompared_ThenTheyAreNotEqual()
        {
            var package1v1 = CreatePackageIdentity("Package1", "1.0");
            var package1v2 = CreatePackageIdentity("Package1", "2.0");

            Assert.AreNotEqual(package1v1,package1v2);
        }

        [Test]
        public void WhenTwoPackagesWithADifferentNameAndSameVersionAreCompared_ThenTheyAreNotEqual()
        {
            var package1 = CreatePackageIdentity("Package1", "1.0");
            var package2 = CreatePackageIdentity("Package2", "1.0");

            Assert.AreNotEqual(package1,package2);
        }

        static PackageIdentity CreatePackageIdentity(string packageId, string packageVersion)
        {
            var version = VersionFactory.CreateSemanticVersion(packageVersion);
            return new PackageIdentity(new PackageId(packageId), version, new PackagePath($"C:\\{packageId}.{packageVersion}.zip"));
        }
    }
}