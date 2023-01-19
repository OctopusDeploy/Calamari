using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Decorators;
using Calamari.Common.Features.Packages.Decorators.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages.ArchiveLimits
{
    [TestFixture]
    public class LogArchiveMetricsDecoratorFixture : CalamariFixture
    {
        readonly string[] testZipPath = { "Fixtures", "Integration", "Packages", "Samples", "Acme.Core.1.0.0.0-bugfix.zip" };

        [Test]
        public void ShouldLogTimedOperation()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: true));
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Any(m => m.FormattedMessage.StartsWith("##octopus[calamari-timed-operation")), "Extract operation should be timed.");
        }

        [Test]
        public void ShouldLogArchiveMetricsIfPossible()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: true));
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Count(m => m.FormattedMessage.StartsWith("##octopus[calamari-deployment-metric")) == 3, "Three deployment metrics should be captured.");
        }

        [Test]
        public void ShouldNotLogWhenFeatureFlagTurnedOff()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: false));
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Any(m => m.FormattedMessage.StartsWith("##octopus[calamari-timed-operation")) == false, "Extract operation should not be timed when feature flag is disabled.");
            Assert.That(Log.Messages.Count(m => m.FormattedMessage.StartsWith("##octopus[calamari-deployment-metric")) == 0, "No deployment metrics should be captured when feature flag is disabled.");
        }

        [Test]
        public void ShouldNotLogWhenFeatureFlagNotPresent()
        {
            var extractor = GetExtractor(new CalamariVariables());
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(Log.Messages.Any(m => m.FormattedMessage.StartsWith("##octopus[calamari-timed-operation")) == false, "Extract operation should not be timed when feature flag is disabled.");
            Assert.That(Log.Messages.Count(m => m.FormattedMessage.StartsWith("##octopus[calamari-deployment-metric")) == 0, "No deployment metrics should be captured when feature flag is disabled.");
        }

        IPackageExtractor GetExtractor(CalamariVariables variables)
        {
            return new NullExtractor().WithExtractionLimits(Log, variables);
        }

        static CalamariVariables WithVariables(bool featureFlagEnabled = true)
        {
            return new CalamariVariables
            {
                { KnownVariables.Package.ArchiveLimits.MetricsEnabled, featureFlagEnabled.ToString() },
            };
        }
    }
}
