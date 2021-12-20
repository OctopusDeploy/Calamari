using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    public class PackageIdentityFactoryFixture
    {
        [TestCase("Package", null)]
        [TestCase(null, "1.0")]
        [TestCase(null, null)]
        public void WhenVariablesMissing_ThenThrowException(string packageId, string version)
        {
            var variables = new CalamariVariables();
            variables.Add(PackageVariables.PackageId, packageId);
            variables.Add(PackageVariables.PackageVersion, version);

            var factory = new PackageIdentityFactory(new ITryToDiscoverVersionFormat[] { new CommandLineVersionFormatDiscovery(), new PackagePathVersionFormatDiscovery(), new JournalVersionFormatDiscovery() });

            Assert.Throws(Is.TypeOf<Exception>().And.Message.Contains("not found").IgnoreCase,
                          () => factory.CreatePackageIdentity(new Journal(null, null, null, null, variables, null), variables, new string[0]));
        }
    }
}