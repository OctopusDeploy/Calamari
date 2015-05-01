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
    [Category(TestEnvironment.CompatableOS.All)]
    [Category("ROB")]
    public class ConfigurationTransformsFixture : CalamariFixture
    {
        readonly string FixtureDirectory = TestEnvironment.GetTestPath("Fixtures", "ConfigurationTransforms");

        private string GetFixtureResouce(params string[] paths)
        {
            return Path.Combine(FixtureDirectory, Path.Combine(paths));
        }
        
        [Test]
        public void WebReleaseConfig()
        {
            var text = PerformTest(GetFixtureResouce("Samples","Web.config"), GetFixtureResouce("Samples","Web.Release.config"));
            var contents = XDocument.Parse(text);

            Assert.IsNull(GetDebugAttribute(contents));
            Assert.AreEqual(GetAppSettingsValue(contents).Value, "Release!");
            Assert.IsNull(GetCustomErrors(contents));
        }

        string PerformTest(string configurationFile, string transformFile)
        {
            var temp = Path.GetTempFileName();
            File.Copy(configurationFile, temp, true);
            
            using (new TemporaryFile(temp))
            {
                var substituter = new ConfigurationTransformer();
                substituter.PerformTransform(temp, transformFile, temp);
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
