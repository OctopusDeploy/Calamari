using System.IO;
using System.Linq;
using System.Xml.Linq;
using Assent;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationTransforms
{
    [TestFixture]
    public class ConfigurationTransformsFixture : CalamariFixture
    {
        InMemoryLog log;
        ConfigurationTransformer configurationTransformer;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            var variables = new CalamariVariables();
            configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);
        }

        [Test]
        public void XmlWithNamespacesIsCorrectlyProcessed()
        {
            var text = PerformTest(GetFixtureResource("Samples", "nlog.config"), GetFixtureResource("Samples", "nlog.Release.config"));
            var document = XDocument.Parse(text);

            this.Assent(document.ToString(), AssentConfiguration.Default);
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        public void WebReleaseConfig()
        {
            var text = PerformTest(GetFixtureResource("Samples", "Web.config"), GetFixtureResource("Samples", "Web.Release.config"));
            var contents = XDocument.Parse(text);

            Assert.IsNull(GetDebugAttribute(contents));
            Assert.AreEqual(GetAppSettingsValue(contents).Value, "Release!");
            Assert.IsNull(GetCustomErrors(contents));
            log.Messages.Should().NotContain(m => m.Level == InMemoryLog.Level.Error, "Should not log errors");
            log.Messages.Should().NotContain(m => m.Level == InMemoryLog.Level.Warn, "Should not log warnings");
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        [ExpectedException(typeof(System.Xml.XmlException))]
        public void ShouldThrowExceptionForBadConfig()
        {
            PerformTest(GetFixtureResource("Samples", "Bad.config"), GetFixtureResource("Samples", "Web.Release.config"));
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        public void ShouldSupressExceptionForBadConfig_WhenFlagIsSet()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Package.IgnoreConfigTransformationErrors, "true");
            configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);

            PerformTest(GetFixtureResource("Samples", "Bad.config"), GetFixtureResource("Samples", "Web.Release.config"));
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        [ExpectedException(typeof(CommandException))]
        public void ShouldThrowExceptionForTransformWarnings()
        {
            PerformTest(GetFixtureResource("Samples", "Web.config"), GetFixtureResource("Samples", "Web.Warning.config"));
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        public void ShouldSuppressExceptionForTransformWarnings_WhenFlagIsSet()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Package.TreatConfigTransformationWarningsAsErrors, "false");
            configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);

            PerformTest(GetFixtureResource("Samples", "Web.config"), GetFixtureResource("Samples", "Web.Warning.config"));
        }

        [Test]
        [RequiresMonoVersion423OrAbove] //Bug in mono < 4.2.3 https://bugzilla.xamarin.com/show_bug.cgi?id=19426
        public void ShouldShowMessageWhenResultIsInvalidXml()
        {
            PerformTest(GetFixtureResource("Samples", "Web.config"), GetFixtureResource("Samples", "Web.Empty.config"));
            log.Messages.Where(m => m.Level == InMemoryLog.Level.Error)
                .Select(m => m.MessageFormat)
                .Should()
                .Contain("The XML configuration file {0} no longer has a root element and is invalid after being transformed by {1}");
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
