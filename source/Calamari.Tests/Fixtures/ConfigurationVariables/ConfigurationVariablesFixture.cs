using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationVariables
{
    [TestFixture]
    public class ConfigurationVariablesFixture : CalamariFixture
    {
        ConfigurationVariablesReplacer configurationVariablesReplacer;
        CalamariVariables variables;

        [SetUp]
        public void SetUp()
        {
            variables = new CalamariVariables();
            configurationVariablesReplacer = new ConfigurationVariablesReplacer(variables, new InMemoryLog());
        }

        [Test]
        public void DoesNotAddXmlHeader()
        {
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResource("Samples", "NoHeader.config"), variables);
            Assert.That(text, Does.StartWith("<configuration"));
        }

        [Test]
        public void SupportsNamespaces()
        {
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResource("Samples","CrazyNamespace.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='WelcomeMessage']").Attribute("value").Value);
            Assert.AreEqual("C:\\Log.txt", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='LogFile']").Attribute("value").Value);
            Assert.AreEqual("", contents.XPathSelectElement("//*[local-name()='appSettings']/*[local-name()='add'][@key='DatabaseConnection']").Attribute("value").Value);
        }

        [Test]
        public void ReplacesAppSettings()
        {
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResource("Samples", "App.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//appSettings/add[@key='WelcomeMessage']").Attribute("value").Value);
            Assert.AreEqual("C:\\Log.txt", contents.XPathSelectElement("//appSettings/add[@key='LogFile']").Attribute("value").Value);
            Assert.AreEqual("", contents.XPathSelectElement("//appSettings/add[@key='DatabaseConnection']").Attribute("value").Value);
        }

        [Test]
        public void ReplacesStronglyTypedAppSettings()
        {
            variables.Set("WelcomeMessage", "Hello world");
            variables.Set("LogFile", "C:\\Log.txt");
            variables.Set("DatabaseConnection", null);

            var text = PerformTest(GetFixtureResource("Samples", "StrongTyped.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Hello world", contents.XPathSelectElement("//AppSettings.Properties.Settings/setting[@name='WelcomeMessage']/value").Value);
        }

        [Test]
        public void ReplacesConnectionStrings()
        {
            variables.Set("MyDb1", "Server=foo");
            variables.Set("MyDb2", "Server=bar&bar=123");

            var text = PerformTest(GetFixtureResource("Samples", "App.config"), variables);

            var contents = XDocument.Parse(text);

            Assert.AreEqual("Server=foo", contents.XPathSelectElement("//connectionStrings/add[@name='MyDb1']").Attribute("connectionString").Value);
            Assert.AreEqual("Server=bar&bar=123", contents.XPathSelectElement("//connectionStrings/add[@name='MyDb2']").Attribute("connectionString").Value);
        }

        [Test]
        [ExpectedException(typeof (System.Xml.XmlException))]
        public void ShouldThrowExceptionForBadConfig()
        {
            PerformTest(GetFixtureResource("Samples", "Bad.config"), variables);
        }

        [Test]
        public void ShouldSuppressExceptionForBadConfig_WhenFlagIsSet()
        {
            variables.AddFlag(KnownVariables.Package.IgnoreVariableReplacementErrors, true);
            configurationVariablesReplacer = new ConfigurationVariablesReplacer(variables, new InMemoryLog());
            PerformTest(GetFixtureResource("Samples", "Bad.config"), variables);
        }

        string PerformTest(string sampleFile, IVariables variables)
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
