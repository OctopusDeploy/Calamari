using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageExtractorFactoryFixture : CalamariFixture
    {
        GenericPackageExtractor factory;

        [SetUp]
        public void SetUp()
        {
            factory = new GenericPackageExtractor();
        }

        [Test]
        [TestCase("tar.gz", typeof(TarGzipPackageExtractor))]
        [TestCase("tar.bz2", typeof(TarBzipPackageExtractor))]
        [TestCase("tar", typeof(TarPackageExtractor))]
        [TestCase("zip", typeof(ZipPackageExtractor))]
        [TestCase("nupkg", typeof(OpenPackagingConventionExtractor))]
        public void GettingFileByExtension(string extension, Type expectedType)
        {
            var extractor = factory.GetExtractor("foo."+ extension);

            Assert.AreEqual(expectedType, extractor.GetType());
        }


        [Test]
        [ExpectedException(typeof(FileFormatException), ExpectedMessage = "Package is missing file extension. This is needed to select the correct extraction algorithm.")]
        public void FileWithNoExtensionThrowsError()
        {
            factory.GetExtractor("blah");
        }


        [Test]
        [ExpectedException(typeof(FileFormatException), ExpectedMessage = "Unsupported file extension \".7z\"")]
        public void FileWithUnsupportedExtensionThrowsError()
        {
            factory.GetExtractor("blah.7z");
        }

        private string GetFileName(string extension)
        {
            return GetFixtureResouce("Samples", "Acme.Core.1.0.0.0-bugfix" + "." + extension);
        }
    }
}
