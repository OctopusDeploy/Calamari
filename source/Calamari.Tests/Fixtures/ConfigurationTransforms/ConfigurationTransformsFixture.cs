using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationTransforms
{
    [TestFixture]
    public class ConfigurationTransformsFixture : CalamariFixture
    {
        ConfigurationTransformer configurationTransformer;

        [SetUp]
        public void SetUp()
        {
            configurationTransformer = new ConfigurationTransformer();
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)] //Problem with XML on Linux
        public void WebReleaseConfig()
        {
            var text = PerformTest(GetFixtureResouce("Samples","Web.config"), GetFixtureResouce("Samples","Web.Release.config"));
            var contents = XDocument.Parse(text);

            Assert.IsNull(GetDebugAttribute(contents));
            Assert.AreEqual(GetAppSettingsValue(contents).Value, "Release!");
            Assert.IsNull(GetCustomErrors(contents));
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)] //Problem with XML on Linux
        [ExpectedException(typeof(System.Xml.XmlException))]
        public void ShouldThrowExceptionForBadConfig()
        {
            PerformTest(GetFixtureResouce("Samples", "Bad.config"), GetFixtureResouce("Samples", "Web.Release.config"));
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)] //Problem with XML on Linux
        public void ShouldSupressExceptionForBadConfig()
        {
            configurationTransformer = new ConfigurationTransformer(true);
            PerformTest(GetFixtureResouce("Samples", "Bad.config"), GetFixtureResouce("Samples", "Web.Release.config"));
        }

        string PerformTest(string configurationFile, string transformFile)
        {
            var temp = Path.GetTempFileName();
            File.Copy(configurationFile, temp, true);
            
            using (new TemporaryFile(temp))
            {
                configurationTransformer.PerformTransform(temp, transformFile, temp);
                return File.ReadAllText(temp);
            }
        }

        static XAttribute GetDebugAttribute(XDocument document)
        {
            return document.Descendants("compilation").First().Attribute("debug");
        }

        static XAttribute GetAppSettingsValue(XDocument document)
        {
            return document.Descendants("appSettings").Descendants("add").First().Attribute("value");
        }

        XElement GetCustomErrors(XDocument document)
        {
            return document.Descendants("customErrors").FirstOrDefault();
        }
    }
}
