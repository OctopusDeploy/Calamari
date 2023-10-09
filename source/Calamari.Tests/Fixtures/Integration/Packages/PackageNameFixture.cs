using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Packages;
using Calamari.Integration.Packages;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageNameFixture
    {

        [Test]
        public void ToCachedFileName_MavenEncodedChars()
        {
            var filename = PackageName.ToCachedFileName("My/Package", VersionFactory.CreateMavenVersion("12:8"), ".jar");

            var scrubbedFilename = Regex.Replace(filename, "[0-9A-F]{32}", "<CACHE-GUID>");
            Assert.AreEqual("My%2FPackage@M12%3A8@<CACHE-GUID>.jar", scrubbedFilename);
        }

        [Test]
        public void ToCachedFileName_Semver()
        {
            var filename = PackageName.ToCachedFileName("My/Package", VersionFactory.CreateSemanticVersion("12.32.1-meta+data"), ".zip");

            var scrubbedFilename = Regex.Replace(filename, "[0-9A-F]{32}", "<CACHE-GUID>");
            Assert.AreEqual("My%2FPackage@S12.32.1-meta+data@<CACHE-GUID>.zip", scrubbedFilename);
        }


        [Test]
        public void FromFile_SimpleConversion()
        {
            var details = PackageName.FromFile("blah/MyPackage@S1.0.0@XXXYYYZZZ.zip");
            Assert.AreEqual("MyPackage", details.PackageId);
            Assert.AreEqual(new SemanticVersion("1.0.0"), details.Version);
            Assert.AreEqual(".zip", details.Extension);
        }

        [Test]
        public void FromFile_EncodedCharacters()
        {
            var details = PackageName.FromFile("blah/My%2FPackage@S1.0.0+CAT@XXXYYYZZZ.zip");
            Assert.AreEqual("My/Package", details.PackageId);
            Assert.AreEqual(new SemanticVersion("1.0.0+CAT"), details.Version);
            Assert.AreEqual(".zip", details.Extension);
        }

        [Test]
        public void FromFile_MavenVersion()
        {
            var details = PackageName.FromFile("blah/pkg@M1.0.0%2BCAT@XXXYYYZZZ.jar");
            Assert.AreEqual("pkg", details.PackageId);
            Assert.AreEqual(VersionFactory.CreateMavenVersion("1.0.0+CAT"), details.Version);
            Assert.AreEqual(".jar", details.Extension);
        }

        [Test]
        public void FromFile_OldSchoolFileType()
        {
            var details = PackageName.FromFile("blah/MyPackage.1.0.8-cat+jat.jar");
            Assert.AreEqual("MyPackage", details.PackageId);
            Assert.AreEqual(VersionFactory.CreateSemanticVersion("1.0.8-cat+jat"), details.Version);
            Assert.AreEqual(".jar", details.Extension);
        }

        [Test]
        [TestCaseSource(nameof(PackageNameTestCases))]
        public void FromFile(string filename, string extension, string packageId, IVersion version)
        {
            var details = PackageName.FromFile($"blah/{filename}");
            Assert.AreEqual(packageId, details.PackageId);
            Assert.AreEqual(version, details.Version);
            Assert.AreEqual(extension, details.Extension);
        }

        [Test]
        public void FromFile_InvalidVersionType()
        {
            Assert.Throws<Exception>(() => PackageName.FromFile("blah/pkg@1.0.0%2BCAT@XXXYYYZZZ.jar"));
        }

        [Test]
        public void FromFile_InvalidSemVer()
        {
            Assert.Throws<Exception>(() => PackageName.FromFile("blah/pkg@S1.D.0%2BCAT@XXXYYYZZZ.jar"));
            
        }

        [Test]
        public void FromFile_UnknownInvalidVersionType()
        {
            Assert.Throws<Exception>(() => PackageName.FromFile("blah/pkg@1.0.0%2BCAT@XXXYYYZZZ.jar"));
        }

        [Test]
        [TestCase("packageId", "packageId")]
        [TestCase("", "")]
        [TestCase("packageId-with-hyphen", "packageId-with-hyphen")]
        [TestCase("folder/packageId-with-hyphen", "packageId-with-hyphen")]
        [TestCase("folder/packageId", "packageId")]
        [TestCase("folder/subfolder/packageId", "packageId")]
        public void ExtractPackageNameFromPathedPackageId(string actual, string expected)
        {
            PackageName.ExtractPackageNameFromPathedPackageId(actual).Should().Be(expected);
        }
        
        

        static IEnumerable<object> PackageNameTestCases()
        {
            var files = new (string filename, string version, string packageId)[]
            {
                ("foo.1.0.0", "1.0.0", "foo"),
                ("foo.1.0.0-tag", "1.0.0-tag", "foo"),
                ("foo.1.0.0-tag-release.tag", "1.0.0-tag-release.tag", "foo"),
                ("foo.1.0.0+buildmeta", "1.0.0+buildmeta", "foo"),
                ("foo.1.0.0-tag-release.tag+buildmeta", "1.0.0-tag-release.tag+buildmeta", "foo"),
            };

            var extensions = new []
            {
                ".zip", 
                ".nupkg",
                ".tar",
                ".tar.gz",
                ".tar.bz2", 
            };

            foreach (var file in files)
            {
                foreach (var extension in extensions)
                {
                    yield return new TestCaseData($"{file.filename}{extension}", extension, file.packageId, VersionFactory.CreateSemanticVersion(file.version));
                }
            }
        }
    }
}