using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using TestCommandLineRunner = Calamari.Testing.Helpers.TestCommandLineRunner;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class CombinedPackageExtractorFixture : CalamariFixture
    {
        [Test]
        [TestCaseSource(nameof(PackageNameTestCases))]
        public void GettingFileByExtension(string filename, Type expectedType)
        {
            var combinedExtractor = CreateCombinedPackageExtractor();
            var specificExtractor = combinedExtractor.GetExtractor(filename);
            if (specificExtractor is ExtractionLimitsDecorator decorator)
                specificExtractor = decorator.ConcreteExtractor;

            Assert.AreEqual(expectedType, specificExtractor.GetType());
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
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Package is missing file extension. This is needed to select the correct extraction algorithm.")]
        public void FileWithNoExtensionThrowsError()
        {
            CreateCombinedPackageExtractor().GetExtractor("blah");
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unsupported file extension `.7z`")]
        public void FileWithUnsupportedExtensionThrowsError()
        {
            CreateCombinedPackageExtractor().GetExtractor("blah.1.0.0.7z");
        }

        static CombinedPackageExtractor CreateCombinedPackageExtractor()
        {
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            var combinedExtractor = new CombinedPackageExtractor(log, variables, new TestCommandLineRunner(log, variables));
            return combinedExtractor;
        }
    }
}
