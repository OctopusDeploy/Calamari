using System;
using System.IO;
using Assent;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Tests.Helpers;
using NUnit.Framework;

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
            var variables = new CalamariVariables();
            variables.Set("MyMessage", "Hello world");
            variables.Set("EmailSettings:SmtpHost", "localhost");
            variables.Set("EmailSettings:SmtpPort", "23");
            variables.Set("EmailSettings:DefaultRecipients:To", "paul@octopus.com");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            var variables = new CalamariVariables();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("IThinkOctopusIsGreat", "Yes, I do!");
            variables.Set("OctopusRocks", "This is ignored");
            variables.Set("Octopus.Rocks", "So is this");
            variables.Set("Octopus:Section", "Should work");

            var replaced = Replace(variables, existingFile: "appsettings.ignore-octopus.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelArray()
        {
            var variables = new CalamariVariables();
            variables.Set("0:Property", "NewValue");

            var replaced = Replace(variables, existingFile: "appsettings.top-level-array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldKeepExistingValues()
        {
            var variables = new CalamariVariables();
            variables.Set("MyMessage", "Hello world!");
            variables.Set("EmailSettings:SmtpPort", "24");
            variables.Set("EmailSettings:DefaultRecipients:Cc", "damo@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.existing-expected.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldMatchAndReplaceIgnoringCase()
        {
            var variables = new CalamariVariables();
            variables.Set("mymessage", "Hello! world!");
            variables.Set("EmailSettings:Defaultrecipients:To", "mark@octopus.com");
            variables.Set("EmailSettings:defaultRecipients:Cc", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithAnEmptyString()
        {
            var variables = new CalamariVariables();
            variables.Set("MyMessage", "");

            var replaced = Replace(variables, existingFile: "appsettings.single.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithNull()
        {
            var variables = new CalamariVariables();
            variables.Set("MyMessage", null);

            var replaced = Replace(variables, existingFile: "appsettings.single.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            var variables = new CalamariVariables();
            variables.Set("EnvironmentVariables:Hosting:Environment", "Production");

            var replaced = Replace(variables, existingFile: "appsettings.colon-in-name.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWholeObject()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:DefaultRecipients", @"{""To"": ""rob@octopus.com"", ""Cc"": ""henrik@octopus.com""}");

            var replaced = Replace(variables, existingFile: "appsettings.simple.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceElementInArray()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:DefaultRecipients:1", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplacePropertyOfAnElementInArray()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:DefaultRecipients:1:Email", "henrik@octopus.com");

            var replaced = Replace(variables, existingFile: "appsettings.object-array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceEntireArray()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:DefaultRecipients", @"[""mike@octopus.com"", ""henrik@octopus.com""]");

            var replaced = Replace(variables, existingFile: "appsettings.array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceNumber()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:SmtpPort", "8023");

            var replaced = Replace(variables, existingFile: "appsettings.array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }


        [Test]
        public void ShouldReplaceDecimal()
        {
            var variables = new CalamariVariables();
            variables.Set("DecimalValue", "50.0");
            variables.Set("FloatValue", "456e-5");
            variables.Set("StringValue", "60.0");
            variables.Set("IntegerValue", "70");

            var replaced = Replace(variables, existingFile: "appsettings.decimals.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceBoolean()
        {
            var variables = new CalamariVariables();
            variables.Set("EmailSettings:UseProxy", "true");

            var replaced = Replace(variables, existingFile: "appsettings.array.json");
            this.Assent(replaced, TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        string Replace(IVariables variables, string existingFile = null)
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
    }
}
