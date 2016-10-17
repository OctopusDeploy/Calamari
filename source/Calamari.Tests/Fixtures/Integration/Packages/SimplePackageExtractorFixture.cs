using System.IO;
using Calamari.Integration.Packages;
using Calamari.Tests.Fixtures.Util;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class SimplePackageExtractorFixture
    {
        SimplePackageExtractor extractor;

        [SetUp]
        public void SetUp()
        {
            extractor = Substitute.For<SimplePackageExtractor>();
            extractor.Extensions.Returns(new string[] { ".tar.gz" });
        }

        [Test]
        public void MetadataExtractedWhenHashPresent()
        {
            var metaData = extractor.GetMetadata("octofxjs.2.3.41.tar.gz-e7e75d07-6b62-4219-81c6-121698876868");

            Assert.AreEqual("octofxjs", metaData.Id);
            Assert.AreEqual("2.3.41", metaData.Version);
            Assert.AreEqual(".tar.gz", metaData.FileExtension);
        }

        [Test]
        public void MetadataExtractedWhenHashAbsent()
        {
            var metaData = extractor.GetMetadata("octofxjs.2.3.41.tar.gz");

            Assert.AreEqual("octofxjs", metaData.Id);
            Assert.AreEqual("2.3.41", metaData.Version);
            Assert.AreEqual(".tar.gz", metaData.FileExtension);
        }

        [Test]
        [ExpectedException(typeof(FileFormatException))]
        public void ThrowsWhenUnknownExtension()
        {
            extractor.GetMetadata("octofxjs.2.3.41.doc");
        }

        [Test]
        [ExpectedException(typeof(FileFormatException))]
        public void ThrowsWhenInvalidSeverVersion()
        {
            extractor.GetMetadata("octofxjs.2.3.other.tar.gz");
        }

        [Test]
        [ExpectedException(typeof(FileFormatException))]
        public void ThrowsWhenMissingVersion()
        {
            extractor.GetMetadata("octofxjs.tar.gz");
        }

        [Test]
        [ExpectedException(typeof(FileFormatException))]
        public void ThrowsWhenMissingPackageId()
        {
            extractor.GetMetadata("2.3.41.doc");
        }

    }
}
