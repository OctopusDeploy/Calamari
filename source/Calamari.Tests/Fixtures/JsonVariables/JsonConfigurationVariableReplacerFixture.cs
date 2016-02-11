using System;
using System.IO;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.JsonVariables
{
    [TestFixture]
    public class JsonConfigurationVariableReplacerFixture : CalamariFixture
    {
        JsonConfigurationVariableReplacer configurationVariableReplacer;

        [SetUp]
        public void SetUp()
        {
            configurationVariableReplacer = new JsonConfigurationVariableReplacer();
        }

        [Test]
        public void ShouldReplaceInSimpleFile()
        {
            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world");
            variables.Set("EmailSettings:SmtpHost", "localhost");
            variables.Set("EmailSettings:SmtpPort", "23");
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "mike@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            AssertJsonEquivalent(replaced, "appsettings.simple.json");
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world");
            variables.Set("IThinkOctopusIsGreat", "Yes, I do");
            variables.Set("OctopusRocks", "This is ignored");
            variables.Set("Octopus.Rocks", "So is this");

            var replaced = Replace(variables, existingFile: "appsettings.ignore-octopus.json");
            AssertJsonEquivalent(replaced, "appsettings.ignore-octopus.json");
        }

        [Test]
        public void ShouldWarnAndIgnoreAmbiguousSettings()
        {
            var variables = new VariableDictionary();
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients", "test@test.com");

            using (var proxyLog = new ProxyLog())
            {
                var replaced = Replace(variables, existingFile: "appsettings.ambiguous.json");
                AssertJsonEquivalent(replaced, "appsettings.ambiguous.json");
                proxyLog.AssertContains("Unable to set value for EmailSettings:DefaultRecipients:To. The property at EmailSettings.DefaultRecipients is a String.");
            }
        }

        [Test]
        public void ShouldKeepExistingValues()
        {
            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("EmailSettings:SmtpPort", "24");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "damo@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.existing-expected.json");
            AssertJsonEquivalent(replaced, "appsettings.existing-expected.json");
        }

        string Replace(VariableDictionary variables, string existingFile = null)
        {
            var temp = Path.GetTempFileName();
            if (existingFile != null)
                File.Copy(GetFixtureResouce("Samples", existingFile), temp, true);

            using (new TemporaryFile(temp))
            {
                configurationVariableReplacer.ModifyJsonFile(temp, variables);
                return File.ReadAllText(temp);
            }
        }

        void AssertJsonEquivalent(string replaced, string sampleFile)
        {
            var expected = File.ReadAllText(GetFixtureResouce("Samples", sampleFile));

            var replacedJson = JToken.Parse(replaced);
            var expectedJson = JToken.Parse(expected);

            if (!JToken.DeepEquals(replacedJson, expectedJson))
            {
                Console.WriteLine("Expected:");
                Console.WriteLine(expectedJson.ToString(Formatting.Indented));

                Console.WriteLine("Replaced:");
                Console.WriteLine(replacedJson.ToString(Formatting.Indented));

                Assert.Fail("Replaced JSON did not match expected JSON");                
            }
        } 
    }
}
