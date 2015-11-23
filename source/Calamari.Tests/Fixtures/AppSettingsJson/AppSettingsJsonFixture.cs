using System;
using System.IO;
using Calamari.Integration.AppSettingsJson;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.AppSettingsJson
{
    [TestFixture]
    public class AppSettingsJsonFixture : CalamariFixture
    {
        AppSettingsJsonGenerator appSettings;

        [SetUp]
        public void SetUp()
        {
            appSettings = new AppSettingsJsonGenerator();
        }

        [Test]
        public void ShouldGenerateSimpleFile()
        {
            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world");
            variables.Set("EmailSettings:SmtpHost", "localhost");
            variables.Set("EmailSettings:SmtpPort", "23");
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "mike@octopus.com");

            var generated = Generate(variables);
            AssertJsonEquivalent(generated, "appsettings.simple.json");
        }

        [Test]
        public void ShouldKeepExistingValues()
        {
            var variables = new VariableDictionary();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("EmailSettings:SmtpPort", "24");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "damo@octopus.com");

            var generated = Generate(variables, existingFile: "appsettings.existing-expected.json");
            AssertJsonEquivalent(generated, "appsettings.existing-expected.json");
        }

        string Generate(VariableDictionary variables, string existingFile = null)
        {
            var temp = Path.GetTempFileName();
            if (existingFile != null)
                File.Copy(GetFixtureResouce("Samples", existingFile), temp, true);

            using (new TemporaryFile(temp))
            {
                appSettings.Generate(temp, variables);
                return File.ReadAllText(temp);
            }
        }

        void AssertJsonEquivalent(string generated, string sampleFile)
        {
            var expected = File.ReadAllText(GetFixtureResouce("Samples", sampleFile));

            var generatedJson = JToken.Parse(generated);
            var expectedJson = JToken.Parse(expected);

            if (!JToken.DeepEquals(generatedJson, expectedJson))
            {
                Console.WriteLine("Expected:");
                Console.WriteLine(expectedJson.ToString(Formatting.Indented));

                Console.WriteLine("Generated:");
                Console.WriteLine(generatedJson.ToString(Formatting.Indented));

                Assert.Fail("Generated JSON did not match expected JSON");                
            }
        } 
    }
}
