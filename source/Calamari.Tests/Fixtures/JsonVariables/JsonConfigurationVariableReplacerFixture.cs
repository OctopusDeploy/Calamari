using System;
using System.IO;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute.Core;
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
            const string expected = 
                @"{" +
                "  \"MyMessage\": \"Hello world\"," +
                "  \"EmailSettings\": {" +
                "    \"SmtpPort\": \"23\"," +
                "    \"SmtpHost\": \"localhost\"," +
                "    \"DefaultRecipients\": {" +
                "      \"To\": \"paul@octopus.com\"," +
                "      \"Cc\": \"henrik@octopus.com\"" +
                "    }" +
                "  }" +
                "}";

            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world");
            variables.Set("EmailSettings:SmtpHost", "localhost");
            variables.Set("EmailSettings:SmtpPort", "23");
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            AssertJsonEquivalent(replaced, expected);
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            const string expected =
                @"{" +
                "  \"MyMessage\": \"Hello world!\"," +
                "  \"IThinkOctopusIsGreat\": \"Yes, I do!\"" +
                "}";

            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("IThinkOctopusIsGreat", "Yes, I do!");
            variables.Set("OctopusRocks", "This is ignored");
            variables.Set("Octopus.Rocks", "So is this");

            var replaced = Replace(variables, existingFile: "appsettings.ignore-octopus.json");
            AssertJsonEquivalent(replaced, expected);
        }

        [Test]
        public void ShouldWarnAndIgnoreAmbiguousSettings()
        {
            const string expected =
                @"{" +
                "  \"EmailSettings\": {" +
                "    \"DefaultRecipients\": \"henrik@octopus.com\"" +
                "  }" +
                "}";

            var variables = new VariableDictionary();
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients", "henrik@octopus.com");

            using (var proxyLog = new ProxyLog())
            {
                var replaced = Replace(variables, existingFile: "appsettings.ambiguous.json");
                AssertJsonEquivalent(replaced, expected);
                proxyLog.AssertContains("Unable to set value for EmailSettings:DefaultRecipients:To. The property at EmailSettings.DefaultRecipients is a String.");
            }
        }

        [Test]
        public void ShouldKeepExistingValues()
        {
            const string expected =
                @"{" +
                "  \"MyMessage\": \"Hello world!\"," +
                "  \"EmailSettings\": {" +
                "    \"SmtpPort\": \"24\"," +
                "    \"SmtpHost\": \"localhost\"," +
                "    \"DefaultRecipients\": {" +
                "      \"To\": \"paul@octopus.com\"," +
                "      \"Cc\": \"damo@octopus.com\"" +
                "    }" +
                "  }" +
                "}";

            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("EmailSettings:SmtpPort", "24");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "damo@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.existing-expected.json");
            AssertJsonEquivalent(replaced, expected);
        }

        [Test]
        public void ShouldMatchAndReplaceIgnoringCase()
        {
            const string expected =
                @"{" +
                "  \"MyMessage\": \"Hello world\"," +
                "  \"EmailSettings\": {" +
                "    \"SmtpPort\": \"23\"," +
                "    \"SmtpHost\": \"localhost\"," +
                "    \"DefaultRecipients\": {" +
                "      \"To\": \"paul@octopus.com\"," +
                "      \"Cc\": \"henrik@octopus.com\"" +
                "    }" +
                "  }" +
                "}";

            var variables = new VariableDictionary();
            variables.Set("mymessage", "Hello world");
            variables.Set("EmailSettings:SmtpHost", "localhost");
            variables.Set("EmailSettings:SmtpPort", "23");
            variables.Set("EmailSettings:Defaultrecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:defaultRecipients:Cc", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            AssertJsonEquivalent(replaced, expected);
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

        void AssertJsonEquivalent(string replaced, string expected)
        {
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
