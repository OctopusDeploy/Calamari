using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
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

        [TestCase("Package", null)]
        [TestCase(null, "1.0")]
        [TestCase(null, null)]
        public void WhenVariablesMissing_ThenThrowException(string packageId, string version)
        {
            var variables = new CalamariVariables();
            variables.Add(PackageVariables.PackageId, packageId);
            variables.Add(PackageVariables.PackageVersion, version);

            Assert.Throws(Is.TypeOf<Exception>().And.Message.Contains("not found").IgnoreCase,
                          () => PackageIdentity.GetPackageIdentity(new Journal(null, null, null), variables, new string[0]));
        }
    }
}