using System;
using System.Collections.Generic;
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
            extractor = new GenericPackageExtractorFactory(ConsoleLog.Instance).CreateStandardGenericPackageExtractor();
        }

        [Test]
        [TestCaseSource(nameof(PackageNameTestCases))]
        public void GettingFileByExtension(string filename, Type expectedType)
        {
            var extractor = this.extractor.GetExtractor(filename);

            Assert.AreEqual(expectedType, extractor.GetType());
        }

        static IEnumerable<object> PackageNameTestCases()
        {
            var fileNames = new[]
            {
                "foo.1.0.0",
                "foo.1.0.0-tag",
                "foo.1.0.0-tag-release.tag",
                "foo.1.0.0+buildmeta",
                "foo.1.0.0-tag-release.tag+buildmeta",
            };

            var extractorMapping = new (string extension, Type extractor)[]
            {
                ("zip", typeof(ZipPackageExtractor)),
                ("nupkg", typeof(NupkgExtractor)),
                ("tar", typeof(TarPackageExtractor)),
                ("tar.gz", typeof(TarGzipPackageExtractor)),
                ("tar.bz2", typeof(TarBzipPackageExtractor)),
            };

            foreach (var filename in fileNames)
            {
                foreach (var (extension, type) in extractorMapping)
                {
                    yield return new TestCaseData($"{filename}.{extension}", type);
                }
            }
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
