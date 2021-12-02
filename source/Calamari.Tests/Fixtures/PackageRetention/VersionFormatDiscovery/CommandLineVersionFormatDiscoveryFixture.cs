using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class CommandLineVersionFormatDiscoveryFixture
    {
        const string packageVersionFormatParam = "--packageVersionFormat";

        [TestCase(new string[0], VersionFormat.Octopus, false, VersionFormat.Octopus, TestName = "WhenNoArgumentsProvided_ThenReturnDefaultFormatAndFalse")]
        [TestCase(new string[2] {packageVersionFormatParam, "This isn't a valid format."}, VersionFormat.Octopus, false, VersionFormat.Octopus, TestName = "WhenInvalidFormatArgumentProvided_ThenReturnDefaultFormatAndFalse")]
        [TestCase(new string[2] {packageVersionFormatParam, "Maven"}, VersionFormat.Octopus, true, VersionFormat.Maven, TestName = "WhenValidArgumentsProvided_ThenReturnCorrectFormatAndTrue")]
        public void TestArgumentsAndResults(string[] commandLineArgs, VersionFormat defaultFormat, bool expectedResult, VersionFormat expectedFormat)
        {
            var discovery = new CommandLineVersionFormatDiscovery();
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), new CalamariVariables(), Substitute.For<ILog>());
            var success = discovery.TryDiscoverVersionFormat(journal,
                                                             new CalamariVariables(),
                                                             commandLineArgs,
                                                             out var format,
                                                             defaultFormat);

            Assert.That(success == expectedResult);
            Assert.That(format == expectedFormat);
        }
    }
} 