using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Assent;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Substitutions
{
    [TestFixture]
    public class SubstitutionsFixture : CalamariFixture
    {
        static readonly CalamariPhysicalFileSystem FileSystem = CalamariEnvironment.IsRunningOnWindows ? (CalamariPhysicalFileSystem)new WindowsPhysicalFileSystem() : new NixCalamariPhysicalFileSystem();
        static readonly Encoding AnsiEncoding;

        static SubstitutionsFixture()
        {
#if NETCORE
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required to use code pages in .NET Standard
#endif
            AnsiEncoding = Encoding.GetEncoding("windows-1252", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        [Test]
        public void ShouldSubstitute()
        {
            var variables = new CalamariVariables
            {
                ["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local",
                ["ServerEndpoints[FOREXUAT01].Port"] = "1566",
                ["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local",
                ["ServerEndpoints[FOREXUAT02].Port"] = "1566"
            };

            var text = PerformTest(GetFixtureResource("Samples", "Servers.json"), variables).text;

            Assert.That(Regex.Replace(text, "\\s+", ""), Is.EqualTo(@"{""Servers"":[{""Name"":""forexuat01.local"",""Port"":1566},{""Name"":""forexuat02.local"",""Port"":1566}]}"));
        }

        [Test]
        public void ShouldApplyFiltersDuringSubstitution()
        {
            var variables = new CalamariVariables
            {
                ["var"] = "=:'\"\\\r\n\t <>\uFFE6"
            };

            var textAfterReplacement = PerformTest(GetFixtureResource("Samples", "Filters.txt"), variables).text;
            this.Assent(textAfterReplacement, AssentConfiguration.Default);
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

            var input = FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual("utf-16", inputEncoding.WebName);
            Assert.AreEqual("utf-16", result.encoding.WebName);
            Assert.AreEqual(2, result.encoding.GetPreamble().Length); // BOM detected
            result.text.Should().Contain("banknames.ora");
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
        }

        [Test]
        public void ShouldOverrideEncodingIfProvided()
        {
            var filePath = GetFixtureResource("Samples", "UTF16LE.ini");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob",
                [PackageVariables.SubstituteInFilesOutputEncoding] = "utf-8"
            };

            var result = PerformTest(filePath, variables);

            var input = FileSystem.ReadFile(filePath, out var inputEncoding);
            Assert.AreEqual("utf-16", inputEncoding.WebName);
            Assert.AreEqual("utf-8", result.encoding.WebName);
            Assert.AreEqual(3, result.encoding.GetPreamble().Length); // BOM detected
            result.text.Should().Contain("banknames.ora");
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
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
            Assert.AreEqual("utf-16", inputEncoding.WebName);
            Assert.AreEqual("utf-16", result.encoding.WebName);
            result.text.Should().MatchRegex(@"\bFile1=banknames.ora\b");
        }

        [Test]
        public void ShouldDetectUtf8WithNoBom()
        {
            var filePath = GetFixtureResource("Samples", "UTF8.txt");

            var text = FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreEqual(0, encoding.GetPreamble().Length); // No BOM detected
            Assert.AreEqual("utf-8", encoding.WebName);
            text.Should().Contain("\u03C0"); // Pi
        }

        [Test]
        public void ShouldDetectUtf8WithBom()
        {
            var filePath = GetFixtureResource("Samples", "UTF8BOM.txt");

            var text = FileSystem.ReadFile(filePath, out var encoding);
            Assert.AreEqual(3, encoding.GetPreamble().Length); // BOM detected
            Assert.AreEqual("utf-8", encoding.WebName);
            text.Should().Contain("\u03C0"); // Pi
        }

        [Test]
        public void RetainAnsi()
        {
            var filePath = GetFixtureResource("Samples", "ANSI.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables, AnsiEncoding);

            var input = File.ReadAllText(filePath, AnsiEncoding);
            result.text.Should().Contain("\u00F7"); // Division sign
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
        }

        [Test]
        public void RetainAscii()
        {
            var filePath = GetFixtureResource("Samples", "ASCII.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpongeBob"
            };

            var result = PerformTest(filePath, variables, Encoding.ASCII);

            var input = File.ReadAllText(filePath, Encoding.ASCII);
            result.text.Should().Contain(@"plain old ASCII");
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
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

            var input = File.ReadAllText(filePath, Encoding.UTF8);
            Assert.AreEqual(0, result.encoding.GetPreamble().Length); // No BOM detected
            Assert.AreEqual("utf-8", result.encoding.WebName);
            result.text.Should().Contain("\u03C0"); // Pi
            result.text.Should().MatchRegex(@"[^\r]\n"); // Unix LF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
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

            var input = File.ReadAllText(filePath, Encoding.UTF8);
            Assert.AreEqual(3, result.encoding.GetPreamble().Length); // BOM detected
            Assert.AreEqual("utf-8", result.encoding.WebName);
            result.text.Should().Contain("\u03C0"); // Pi
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "SpongeBob"));
        }

        [Test]
        public void AmbiguouslyEncodedInputRetainsUnicodeVariables()
        {
            var filePath = GetFixtureResource("Samples", "ASCII.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpöngeBöb"
            };

            var result = PerformTest(filePath, variables);

            var input = File.ReadAllText(filePath, Encoding.ASCII);
            Assert.AreEqual(0, result.encoding.GetPreamble().Length); // No BOM detected
            Assert.AreEqual("utf-8", result.encoding.WebName);
            result.text.Should().Contain(@"plain old ASCII");
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "Sp\u00F6ngeB\u00F6b"));
        }
        
        [Test]
        public void WhenAnsiCannotRepresentOutputUtf8IsUsed()
        {
            var filePath = GetFixtureResource("Samples", "ANSI.txt");
            var variables = new CalamariVariables
            {
                ["LocalCacheFolderName"] = "SpőngeBőb"
            };

            var result = PerformTest(filePath, variables);

            var input = File.ReadAllText(filePath, AnsiEncoding);
            Assert.AreEqual(0, result.encoding.GetPreamble().Length); // No BOM detected
            Assert.AreEqual("utf-8", result.encoding.WebName);
            result.text.Should().Contain("\u00F7"); // Division sign
            result.text.Should().MatchRegex(@"\r\n"); // DOS CRLF
            result.text.Should().Be(input.Replace("#{LocalCacheFolderName}", "Sp\u0151ngeB\u0151b"));
        }

        (string text, Encoding encoding) PerformTest(string sampleFile, IVariables variables, Encoding expectedResultEncoding = null)
        {
            var temp = Path.GetTempFileName();
            using (new TemporaryFile(temp))
            {
                var substituter = new FileSubstituter(new InMemoryLog(), FileSystem);
                substituter.PerformSubstitution(sampleFile, variables, temp);
                using (var reader = new StreamReader(temp, expectedResultEncoding ?? new UTF8Encoding(false, true), expectedResultEncoding == null))
                {
                    return (reader.ReadToEnd(), reader.CurrentEncoding);
                }
            }
        }
    }
}