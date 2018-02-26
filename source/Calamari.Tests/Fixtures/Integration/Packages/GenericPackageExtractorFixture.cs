using System;
using System.IO;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.NuGet;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class GenericPackageExtractorFixture : CalamariFixture
    {
        GenericPackageExtractor extractor;

        [SetUp]
        public void SetUp()
        {
            extractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();
        }

        [Test]
        [TestCase("tar.gz", typeof(TarGzipPackageExtractor))]
        [TestCase("tar.bz2", typeof(TarBzipPackageExtractor))]
        [TestCase("tar", typeof(TarPackageExtractor))]
        [TestCase("zip", typeof(ZipPackageExtractor))]
        [TestCase("nupkg", typeof(NupkgExtractor))]
        public void GettingFileByExtension(string extension, Type expectedType)
        {
            var extractor = this.extractor.GetExtractor("foo.1.0.0."+ extension);

            Assert.AreEqual(expectedType, extractor.GetType());
        }

        [Test]
        [ExpectedException(typeof(FileFormatException), ExpectedMessage = "Package is missing file extension. This is needed to select the correct extraction algorithm.")]
        public void FileWithNoExtensionThrowsError()
        {
            extractor.GetExtractor("blah");
        }

        [Test]
        [ExpectedException(typeof(FileFormatException), ExpectedMessage = "Unsupported file extension `.7z`")]
        public void FileWithUnsupportedExtensionThrowsError()
        {
            extractor.GetExtractor("blah.1.0.0.7z");
        }
    }
}
