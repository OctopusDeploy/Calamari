using Assent;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class JsonFormatVariableReplacerFixture : VariableReplacerFixture
    {
        public JsonFormatVariableReplacerFixture() : base(new JsonFormatVariableReplacer(CalamariPhysicalFileSystem.GetPhysicalFileSystem()))
        {
        }

        [Test]
        public void ShouldReplaceInSimpleFile()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "Hello world" },
                                    { "EmailSettings:SmtpHost", "localhost" },
                                    { "EmailSettings:SmtpPort", "23" },
                                    { "EmailSettings:DefaultRecipients:To", "paul@octopus.com" },
                                    { "EmailSettings:DefaultRecipients:Cc", "henrik@octopus.com" }
                                },
                                existingFile: "appsettings.simple.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "Hello world!" },
                                    { "IThinkOctopusIsGreat", "Yes, I do!" },
                                    { "OctopusRocks", "This is ignored" },
                                    { "Octopus.Rocks", "So is this" },
                                    { "Octopus:Section", "Should work" }
                                },
                                existingFile: "appsettings.ignore-octopus.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "0:Property", "NewValue" }
                                },
                                existingFile: "appsettings.top-level-array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldKeepExistingValues()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "Hello world!" },
                                    { "EmailSettings:SmtpPort", "24" },
                                    { "EmailSettings:DefaultRecipients:Cc", "damo@octopus.com" }
                                },
                                existingFile: "appsettings.existing-expected.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldMatchAndReplaceIgnoringCase()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "mymessage", "Hello! world!" },
                                    { "EmailSettings:Defaultrecipients:To", "mark@octopus.com" },
                                    { "EmailSettings:defaultRecipients:Cc", "henrik@octopus.com" }
                                },
                                existingFile: "appsettings.simple.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithAnEmptyString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "" }
                                },
                                existingFile: "appsettings.single.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithNull()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", null }
                                },
                                existingFile: "appsettings.single.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EnvironmentVariables:Hosting:Environment", "Production" }
                                },
                                existingFile: "appsettings.colon-in-name.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWholeObject()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"{""To"": ""rob@octopus.com"", ""Cc"": ""henrik@octopus.com""}" }
                                },
                                existingFile: "appsettings.simple.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1", "henrik@octopus.com" }
                                },
                                existingFile: "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplacePropertyOfAnElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1:Email", "henrik@octopus.com" }
                                },
                                existingFile: "appsettings.object-array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceEntireArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"[""mike@octopus.com"", ""henrik@octopus.com""]" }
                                },
                                existingFile: "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceNumber()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:SmtpPort", "8023" }
                                },
                                existingFile: "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceDecimal()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "DecimalValue", "50.0" },
                                    { "FloatValue", "456e-5" },
                                    { "StringValue", "60.0" },
                                    { "IntegerValue", "70" }
                                },
                                existingFile: "appsettings.decimals.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceBoolean()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:UseProxy", "true" }
                                },
                                existingFile: "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }
    }
}