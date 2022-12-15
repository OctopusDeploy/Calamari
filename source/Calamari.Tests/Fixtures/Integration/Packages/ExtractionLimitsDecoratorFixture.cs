using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class ExtractionLimitsDecoratorFixture : CalamariFixture
    {
        ExtractionLimitsDecorator decorator;
        string[] testZipPath = new[] { "Fixtures", "Integration", "Packages", "Samples", "Acme.Core.1.0.0.0-bugfix.zip" };

        [SetUp]
        public void SetUp()
        {
            decorator = new ExtractionLimitsDecorator(new NullExtractor(), Log);
        }

        [Test]
        public void ShouldLogTimedOperation()
        {
            decorator.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Any(m => m.FormattedMessage.StartsWith("##octopus[calamari-timed-operation")), "Extract operation should be timed.");
        }

        [Test]
        public void ShouldLogArchiveMetricsIfPossible()
        {
            decorator.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Count(m => m.FormattedMessage.StartsWith("##octopus[calamari-deployment-metric")) == 3, "Three deployment metrics should be captured.");
        }

        class NullExtractor : IPackageExtractor
        {
            public string[] Extensions => new[] { ".zip" };
            public int Extract(string packageFile, string directory)
            {
                return 1;
            }
        }
    }
}
