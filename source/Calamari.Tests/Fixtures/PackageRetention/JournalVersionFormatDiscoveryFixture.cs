using System;
using System.IO.Packaging;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalVersionFormatDiscoveryFixture
    {

       //TODO: Create more test cases.
        [TestCase("Package1", "1.0", VersionFormat.Maven, "Package1", "1.0", VersionFormat.Octopus, true, VersionFormat.Maven, TestName = "WhenValidArgumentsProvided_ThenReturnCorrectFormatAndTrue")]
        public void TestArgumentsAndResults(string existingPackageId, string existingVersion, VersionFormat existingFormat, string thisPackageId, string thisVersion, VersionFormat defaultFormat, bool expectedResult, VersionFormat expectedFormat)
        {
            var discovery = new JournalVersionFormatDiscovery();
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), Substitute.For<ILog>());
            var package = new PackageIdentity(existingPackageId, existingVersion, existingFormat);

            journal.RegisterPackageUse(package, new ServerTaskId("Task-1"));

            var variables = new CalamariVariables();
            variables.Add(PackageVariables.PackageId, thisPackageId);
            variables.Add(PackageVariables.PackageVersion, thisVersion);

            var success = discovery.TryDiscoverVersionFormat(journal,
                                                             variables,
                                                             new string[0],
                                                             out var format,
                                                             defaultFormat);

            Assert.That(success == expectedResult);
            Assert.That(format == expectedFormat);
        }
    }
}