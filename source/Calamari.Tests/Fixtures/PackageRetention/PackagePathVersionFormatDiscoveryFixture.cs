using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class PackagePathVersionFormatDiscoveryFixture
    {
        [TestCase("Not A Path", VersionFormat.Octopus, false, VersionFormat.Octopus, TestName = "WhenInvalidPathProvided_ThenReturnDefaultFormatAndFalse")]
        [TestCase(@"C:\Tentacle@S1.0.0@D461D13C2427224396258743C8F485E4.zip", VersionFormat.Octopus, true, VersionFormat.Semver, TestName = "WhenValidSemverPathProvided_ThenReturnSemverFormatAndTrue")]
        [TestCase(@"C:\Tentacle@M1.0.0@D461D13C2427224396258743C8F485E4.zip", VersionFormat.Octopus, true, VersionFormat.Maven, TestName = "WhenValidMavenPathProvided_ThenReturnMavenFormatAndTrue")]
        [TestCase(@"C:\Tentacle@S1f.0.0@D461D13C2427224396258743C8F485E4.zip", VersionFormat.Octopus, false, VersionFormat.Octopus, TestName = "WhenInvalidSemverPathProvided_ThenReturnDefaultFormatAndFalse")]
        public void TestArgumentsAndResults(string path, VersionFormat defaultFormat, bool expectedResult, VersionFormat expectedFormat)
        {
            var discovery = new PackagePathVersionFormatDiscovery();
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), Substitute.For<ILog>());
            var variables = new CalamariVariables();
            variables.Add(TentacleVariables.CurrentDeployment.PackageFilePath, path);

            var success = discovery.TryDiscoverVersionFormat(journal,
                                                             variables,
                                                             new string[0],
                                                             out var format,
                                                             VersionFormat.Octopus);

            Assert.That(success == expectedResult);
            Assert.That(format == expectedFormat);
        }
    }
}