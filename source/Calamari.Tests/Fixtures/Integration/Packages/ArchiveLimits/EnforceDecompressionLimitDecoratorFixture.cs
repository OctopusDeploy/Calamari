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
    public class EnforceDecompressionLimitDecoratorFixture : CalamariFixture
    {
        readonly string[] testZipPath = { "Fixtures", "Integration", "Packages", "Samples", "Acme.Core.1.0.0.0-bugfix.zip" };

        [Test]
        [ExpectedException(typeof(ArchiveLimitException))]
        public void ShouldEnforceLimit()
        {
            var extractor = GetExtractor(WithVariables(maximumUncompressedSize: 1));
            extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
        }

        [Test]
        public void ShouldIgnoreNonsenseLimit()
        {
            var extractor = GetExtractor(WithVariables(maximumUncompressedSize: -1));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractWhenUnderLimit()
        {
            var extractor = GetExtractor(WithVariables(maximumUncompressedSize: 1000000000));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractRegardlessOfLimitWithFeatureFlagOff()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: false, maximumUncompressedSize: 1));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        [Test]
        public void ShouldExtractRegardlessOfLimitWithFeatureFlagNotPresent()
        {
            var extractor = GetExtractor(WithVariables(featureFlagEnabled: false, maximumUncompressedSize: 1));
            var extractedFiles = extractor.Extract(TestEnvironment.GetTestPath(testZipPath), TestEnvironment.GetTestPath("extracted"));
            Assert.That(extractedFiles, Is.EqualTo(1));
        }

        IPackageExtractor GetExtractor(CalamariVariables variables)
        {
            return new NullExtractor().WithExtractionLimits(Log, variables);
        }

        static CalamariVariables WithVariables(int maximumUncompressedSize = 1000000000, bool featureFlagEnabled = true)
        {
            return new CalamariVariables
            {
                { KnownVariables.Package.ArchiveLimits.Enabled, featureFlagEnabled.ToString() },
                { KnownVariables.Package.ArchiveLimits.MaximumUncompressedSize, maximumUncompressedSize.ToString() }
            };
        }
    }
}
