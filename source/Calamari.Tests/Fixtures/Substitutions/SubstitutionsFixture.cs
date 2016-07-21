using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Substitutions
{
    [TestFixture]
    public class SubstitutionsFixture : CalamariFixture
    {
        static readonly WindowsPhysicalFileSystem FileSystem = new WindowsPhysicalFileSystem();

        [Test]
        public void ShouldSubstitute()
        {
            var variables = new VariableDictionary();
            variables["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local";
            variables["ServerEndpoints[FOREXUAT01].Port"] = "1566";
            variables["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local";
            variables["ServerEndpoints[FOREXUAT02].Port"] = "1566";
            
            var text = PerformTest(GetFixtureResouce("Samples","Servers.json"), variables).Text;

            Assert.That(Regex.Replace(text, "\\s+", ""), Is.EqualTo(@"{""Servers"":[{""Name"":""forexuat01.local"",""Port"":1566},{""Name"":""forexuat02.local"",""Port"":1566}]}"));
        }

        [Test]
        public void ShouldRetainEncodingIfNoneSet()
        {
            var filePath = GetFixtureResouce("Samples", "UTF16LE.ini");
            var variables = new VariableDictionary();
            variables["LocalCacheFolderName"] = "SpongeBob";

            var result = PerformTest(filePath, variables);

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.Unicode, encoding);
            Assert.AreEqual(Encoding.Unicode, result.Encoding);
            Assert.True(Regex.Match(result.Text, "\\bLocalCacheFolderName=SpongeBob\\b").Success);
        }

        [Test]
        public void ShouldOverrideEncodingIfProvided()
        {

            var filePath = GetFixtureResouce("Samples", "UTF16LE.ini");
            var variables = new VariableDictionary();
            variables[SpecialVariables.Package.SubstituteInFilesOutputEncoding] = "utf-8";

            var encoding = (Encoding)PerformTest(filePath, variables).Encoding;

            Encoding fileEncoding;
            FileSystem.ReadFile(filePath, out fileEncoding);
            Assert.AreEqual(Encoding.Unicode, fileEncoding);
            Assert.AreEqual(Encoding.UTF8, encoding);
        }

        [Test]
        public void ShouldRevertToExistingEncodingIfInvalid()
        {

            var filePath = GetFixtureResouce("Samples", "UTF16LE.ini");
            var variables = new VariableDictionary();
            variables[SpecialVariables.Package.SubstituteInFilesOutputEncoding] = "utf-666";

            var encoding = (Encoding)PerformTest(filePath, variables).Encoding;

            Encoding fileEncoding;
            FileSystem.ReadFile(filePath, out fileEncoding);
            Assert.AreEqual(Encoding.Unicode, fileEncoding);
            Assert.AreEqual(Encoding.Unicode, encoding);
        }

        [Test]
        public void ShouldDetectUTF8WithNoBom()
        {
            var filePath = GetFixtureResouce("Samples", "UTF8.txt");

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreNotEqual(Encoding.UTF8, encoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, encoding.EncodingName); //but are both utf-8
        }

        [Test]
        public void ShouldDetectUTF8WithBom()
        {
            var filePath = GetFixtureResouce("Samples", "UTF8BOM.txt");

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.UTF8, encoding);
        }

        [Test]
        public void ShouldDetectASCII()
        {
            var filePath = GetFixtureResouce("Samples", "ASCII.txt");

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.ASCII, encoding);
        }


        [Test]
        public void ShouldFallBackToDefaultCodePage()
        {
            var filePath = GetFixtureResouce("Samples", "ANSI.txt");

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.Default, encoding);
        }

        [Test]
        public void RetainANSI()
        {
            var filePath = GetFixtureResouce("Samples", "ANSI.txt");
            var variables = new VariableDictionary();
            variables["LocalCacheFolderName"] = "SpongeBob";

            var result = PerformTest(filePath, variables);

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.Default, encoding);
            Assert.AreEqual(Encoding.Default, result.Encoding);
        }

        [Test]
        public void RetainASCII()
        {
            var filePath = GetFixtureResouce("Samples", "ASCII.txt");
            var variables = new VariableDictionary();
            variables["LocalCacheFolderName"] = "SpongeBob";

            var result = PerformTest(filePath, variables);

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.ASCII, encoding);
            Assert.AreEqual(Encoding.ASCII, result.Encoding);
        }

        [Test]
        public void RetainUTF8()
        {
            var filePath = GetFixtureResouce("Samples", "UTF8.txt");
            var variables = new VariableDictionary();
            variables["LocalCacheFolderName"] = "SpongeBob";

            var result = PerformTest(filePath, variables);

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreNotEqual(Encoding.UTF8, encoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, encoding.EncodingName); //but are both utf-8
            Assert.AreNotEqual(Encoding.UTF8, result.Encoding); //not the static encoder (which does bom)
            Assert.AreEqual(Encoding.UTF8.EncodingName, result.Encoding.EncodingName); //but are both utf-8
        }

        [Test]
        public void RetainUTF8Bom()
        {
            var filePath = GetFixtureResouce("Samples", "UTF8BOM.txt");
            var variables = new VariableDictionary();
            variables["LocalCacheFolderName"] = "SpongeBob";

            var result = PerformTest(filePath, variables);

            Encoding encoding;
            FileSystem.ReadFile(filePath, out encoding);
            Assert.AreEqual(Encoding.UTF8, encoding);
            Assert.AreEqual(Encoding.UTF8, result.Encoding);
        }

        dynamic PerformTest(string sampleFile, VariableDictionary variables)
        {
            var temp = Path.GetTempFileName();
            using (new TemporaryFile(temp))
            {
                var substituter = new FileSubstituter(FileSystem);
                substituter.PerformSubstitution(sampleFile, variables, temp);
                Encoding encoding;
                var text = FileSystem.ReadFile(temp, out encoding);
                return new {
                    Text = text,
                    Encoding = encoding
                };
            }
        }
    }
}
