using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Decorators;
using Calamari.Common.Features.Packages.Decorators.ArchiveLimits;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages.ArchiveLimits
{
    [TestFixture]
    public class EnforceCompressionRatioDecoratorFixture : CalamariFixture
    {
        readonly string[] testZipPath = { "Fixtures", "Integration", "Packages", "Samples", "Acme.Core.1.0.0.0-bugfix.zip" };

        [Test]
        [ExpectedException(typeof(ArchiveLimitException))]
        public void ShouldEnforceLimit()
        {
            var extractor =GetExtractor(WithVariables(maximumCompressionRatio: 1));
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void ShouldIgnoreNonsenseLimit(int ratio)
        {
            var extractor = GetExtractor(WithVariables(maximumCompressionRatio: ratio));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractWhenUnderLimit()
        {
            var extractor = GetExtractor(WithVariables(maximumCompressionRatio: 5000));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractRegardlessOfLimitWithFeatureFlagOff()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: false, maximumCompressionRatio: 1));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractRegardlessOfLimitWithFeatureFlagNotPresent()
        {
            var extractor = GetExtractor(new CalamariVariables());
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        IPackageExtractor GetExtractor(CalamariVariables variables)
        {
            return new NullExtractor().WithExtractionLimits(Log, variables);
        }

        static CalamariVariables WithVariables(int maximumCompressionRatio = 5000, bool featureFlagEnabled = true)
        {
            return new CalamariVariables
            {
                { KnownVariables.Package.ArchiveLimits.Enabled, featureFlagEnabled.ToString() },
                { KnownVariables.Package.ArchiveLimits.MaximumCompressionRatio, maximumCompressionRatio.ToString() }
            };
        }
    }
}
