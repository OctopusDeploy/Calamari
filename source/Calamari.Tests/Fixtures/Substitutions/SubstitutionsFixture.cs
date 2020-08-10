using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Assent;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Substitutions
{
    [TestFixture]
    public class SubstitutionsFixture : CalamariFixture
    {
        static readonly CalamariPhysicalFileSystem FileSystem = CalamariEnvironment.IsRunningOnWindows ? (CalamariPhysicalFileSystem)new WindowsPhysicalFileSystem() : new NixCalamariPhysicalFileSystem();

        [Test]
        public void ShouldSubstitute()
        {
            var variables = new CalamariVariables();
            variables["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local";
            variables["ServerEndpoints[FOREXUAT01].Port"] = "1566";
            variables["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local";
            variables["ServerEndpoints[FOREXUAT02].Port"] = "1566";

            var text = PerformTest(GetFixtureResource("Samples", "Servers.json"), variables).text;

            Assert.That(Regex.Replace(text, "\\s+", ""), Is.EqualTo(@"{""Servers"":[{""Name"":""forexuat01.local"",""Port"":1566},{""Name"":""forexuat02.local"",""Port"":1566}]}"));
        }

        [Test]
        public void ShouldApplyFiltersDuringSubstitution()
        {
            var variables = new CalamariVariables
            {
                ["var"] = "=:'\"\\\r\n\t <>"
            };

            var textAfterReplacement = PerformTest(GetFixtureResource("Samples", "Filters.txt"), variables).text;
            this.Assent(textAfterReplacement, TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldIgnoreParserErrors()
        {
            var variables = new CalamariVariables
            {
                ["fox"] = "replaced fox"
            };

            var text = PerformTest(GetFixtureResource("Samples", "ParserErrors.txt"), variables).text;

            // Environment.Newline returning \r\n when running tests on mono, but \n on dotnet core, just replace
            Assert.AreEqual("the quick brown replaced fox jumps over the lazy #{dog\nthe quick brown replaced fox jumps over the lazy #{dog #{", text.Replace("\r\n", "\n"));
        }

        [Test]
        public void ShouldRetainEncodingIfNoneSet()
        {
            var filePath = GetFixtureResource("Samples", "UTF16LE.ini");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.Unicode, inputEncoding);
            Assert.AreEqual(Encoding.Unicode, result.encoding);
            Assert.True(Regex.Match(result.text, "\\bLocalCacheFolderName=SpongeBob\\b").Success);
        }

        [Test]
        public void ShouldOverrideEncodingIfProvided()
        {
            var filePath = GetFixtureResource("Samples", "UTF16LE.ini");
            var variables = new CalamariVariables
            {
                [PackageVariables.SubstituteInFilesOutputEncoding] = "utf-8"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.Unicode, inputEncoding);
            Assert.AreEqual(Encoding.UTF8, result.encoding);
        }

        [Test]
        public void ShouldRevertToExistingEncodingIfInvalid()
        {
            var filePath = GetFixtureResource("Samples", "UTF16LE.ini");
            var variables = new CalamariVariables
            {
                [PackageVariables.SubstituteInFilesOutputEncoding] = "utf-666"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.Unicode, inputEncoding);
            Assert.AreEqual(Encoding.Unicode, result.encoding);
        }

        [Test]
        public void ShouldDetectUtf8WithNoBom()
        {
            var filePath = GetFixtureResource("Samples", "UTF8.txt");

            FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreNotEqual(Encoding.UTF8, encoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, encoding.EncodingName); //but are both utf-8
        }

        [Test]
        public void ShouldDetectUtf8WithBom()
        {
            var filePath = GetFixtureResource("Samples", "UTF8BOM.txt");

            FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreEqual(Encoding.UTF8, encoding);
        }

        [Test]
        public void ShouldDetectAscii()
        {
            var filePath = GetFixtureResource("Samples", "ASCII.txt");

            FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreEqual(Encoding.ASCII, encoding);
        }

        [Test]
        public void ShouldFallBackToDefaultCodePage()
        {
            var filePath = GetFixtureResource("Samples", "ANSI.txt");

            FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreEqual(Encoding.Default, encoding);
        }

        [Test]
        public void RetainAnsi()
        {
            var filePath = GetFixtureResource("Samples", "ANSI.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.Default, inputEncoding);
            Assert.AreEqual(Encoding.Default, result.encoding);
        }

        [Test]
        public void RetainAscii()
        {
            var filePath = GetFixtureResource("Samples", "ASCII.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.ASCII, inputEncoding);
            Assert.AreEqual(Encoding.ASCII, result.encoding);
        }

        [Test]
        public void RetainUtf8()
        {
            var filePath = GetFixtureResource("Samples", "UTF8.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreNotEqual(Encoding.UTF8, inputEncoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, inputEncoding.EncodingName); //but are both utf-8
            Assert.AreNotEqual(Encoding.UTF8, result.encoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, result.encoding.EncodingName); //but are both utf-8
        }

        [Test]
        public void RetainUtf8Bom()
        {
            var filePath = GetFixtureResource("Samples", "UTF8BOM.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables);

            FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual(Encoding.UTF8, inputEncoding);
            Assert.AreEqual(Encoding.UTF8, result.encoding);
        }

        (string text, Encoding encoding) PerformTest(string sampleFile, IVariables variables)
        {
            var temp = Path.GetTempFileName();
            using (new TemporaryFile(temp))
            {
                var substituter = new FileSubstituter(new InMemoryLog(), FileSystem);
                substituter.PerformSubstitution(sampleFile, variables, temp);
                var text = FileSystem.ReadFile(temp, out var encoding);
                return (text, encoding);
            }
        }
    }
}