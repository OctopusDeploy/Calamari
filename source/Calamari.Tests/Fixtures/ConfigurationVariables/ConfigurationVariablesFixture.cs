using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.ConfigurationVariables
{
    [TestFixture]
    public class ConfigurationVariablesFixture : CalamariFixture
    {
        ConfigurationVariablesReplacer configurationVariablesReplacer;

        [SetUp]
        public void SetUp()
        {
            configurationVariablesReplacer = new ConfigurationVariablesReplacer();
        }

        [Test]
        public void DoesNotAddXmlHeader()
        {
            var variables = new VariableDictionary();
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResouce("Samples", "NoHeader.config"), variables);
            Assert.That(text, Does.StartWith("<configuration"));
        }

        [Test]
        public void SupportsNamespaces()
        {
            var variables = new VariableDictionary();
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResouce("Samples","CrazyNamespace.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='WelcomeMessage']").Attribute("value").Value);
            Assert.AreEqual("C:\\Log.txt", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='LogFile']").Attribute("value").Value);
            Assert.AreEqual("", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='DatabaseConnection']").Attribute("value").Value);
        }

        [Test]
        public void ReplacesAppSettings()
        {
            var variables = new VariableDictionary();
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResouce("Samples", "App.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//appSettings/add[@key='WelcomeMessage']").Attribute("value").Value);
            Assert.AreEqual("C:\\Log.txt", contents.XPathSelectElement("//appSettings/add[@key='LogFile']").Attribute("value").Value);
            Assert.AreEqual("", contents.XPathSelectElement("//appSettings/add[@key='DatabaseConnection']").Attribute("value").Value);
        }

        [Test]
        public void ReplacesStronglyTypedAppSettings()
        {
            var variables = new VariableDictionary();
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResouce("Samples", "StrongTyped.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//AppSettings.Properties.Settings/setting[@name='WelcomeMessage']/value").Value);
        }

        [Test]
        public void ReplacesConnectionStrings()
        {
            var variables = new VariableDictionary();
            variables.Set("MyDb1", "Server=foo");
            variables.Set("MyDb2", "Server=bar&bar=123");
            
            var text = PerformTest(GetFixtureResouce("Samples", "App.config"), variables);

            var contents = XDocument.Parse(text);
            
            Assert.AreEqual("Server=foo", contents.XPathSelectElement("//connectionStrings/add[@name='MyDb1']").Attribute("connectionString").Value);
            Assert.AreEqual("Server=bar&bar=123", contents.XPathSelectElement("//connectionStrings/add[@name='MyDb2']").Attribute("connectionString").Value);
        }

        [Test]
        [ExpectedException(typeof (System.Xml.XmlException))]
        public void ShouldThrowExceptionForBadConfig()
        {
            var variables = new VariableDictionary();
            PerformTest(GetFixtureResouce("Samples", "Bad.config"), variables);
        }

        [Test]
        public void ShouldSupressExceptionForBadConfig_WhenFlagIsSet()
        {
            configurationVariablesReplacer = new ConfigurationVariablesReplacer(true);
            var variables = new VariableDictionary();
            PerformTest(GetFixtureResouce("Samples", "Bad.config"), variables);
        }

        string PerformTest(string sampleFile, VariableDictionary variables)
        {
            var temp = Path.GetTempFileName();
            File.Copy(sampleFile, temp, true);
            
            using (new TemporaryFile(temp))
            {
                configurationVariablesReplacer.ModifyConfigurationFile(temp, variables);
                return File.ReadAllText(temp);
            }
        }
    }
}
