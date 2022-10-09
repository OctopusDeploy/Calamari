using System;
using System.Linq;
using System.Text.RegularExpressions;
using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class JsonFormatVariableReplacerFixture : VariableReplacerFixture
    {
        public JsonFormatVariableReplacerFixture() : base((fs, log) => new JsonFormatVariableReplacer(fs, log))
        {
        }

        [Test]
        public void DoesNothingIfThereAreNoVariables()
        {
            this.Assent(Replace(new CalamariVariables(), "appsettings.simple.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void DoesNothingIfThereAreNoMatchingVariables()
        {
            Replace(new CalamariVariables { { "Non-matching", "variable" } }, "appsettings.simple.json");

            Log.MessagesInfoFormatted.Should().Contain(StructuredConfigMessages.NoStructuresFound);
            Log.MessagesVerboseFormatted.Should().NotContain(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*")));
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
                                "appsettings.simple.json"),
                AssentConfiguration.Json);

            Log.MessagesVerboseFormatted.Count(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*"))).Should().Be(5);
            Log.MessagesVerboseFormatted.Should().Contain(StructuredConfigMessages.StructureFound("EmailSettings:SmtpPort"));
            Log.MessagesInfoFormatted.Should().NotContain(StructuredConfigMessages.NoStructuresFound);
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
                                "appsettings.ignore-octopus.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "0:Property", "NewValue" }
                                },
                                "appsettings.top-level-array.json"),
                AssentConfiguration.Json);
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
                                "appsettings.existing-expected.json"),
                AssentConfiguration.Json);
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
                                "appsettings.simple.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceWithAnEmptyString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "" }
                                },
                                "appsettings.single.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceWithNull()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", null }
                                },
                                "appsettings.single.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EnvironmentVariables:Hosting:Environment", "Production" }
                                },
                                "appsettings.colon-in-name.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceWholeObject()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"{""To"": ""rob@octopus.com"", ""Cc"": ""henrik@octopus.com""}" }
                                },
                                "appsettings.simple.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ObjectIsNotModifiedIfTheVariableValueCannotBeParsed()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"{<<<<" }
                                },
                                "appsettings.simple.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1", "henrik@octopus.com" }
                                },
                                "appsettings.array.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplacePropertyOfAnElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1:Email", "henrik@octopus.com" }
                                },
                                "appsettings.object-array.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceEntireArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"[""mike@octopus.com"", ""henrik@octopus.com""]" }
                                },
                                "appsettings.array.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceNumber()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:SmtpPort", "8023" }
                                },
                                "appsettings.array.json"),
                AssentConfiguration.Json);
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
                                "appsettings.decimals.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceBoolean()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:UseProxy", "true" }
                                },
                                "appsettings.array.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldReplaceStructures()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    {"mapping2mapping", "{\"this\": \"is\", \"new\": \"mapping\"}"},
                                    {"sequence2sequence", "[ \"another\", \"sequence\", \"altogether\" ]"},
                                    {"mapping2sequence", "[\"a\",\"sequence\"]"},
                                    {"sequence2mapping", "{  \"this\": \"now\",  \"has\":    \"keys\" }"},
                                    {"mapping2string", "\"no longer a mapping\""},
                                    {"sequence2string", "\"no longer a sequence\""},
                                },
                                "structures.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldFallBackToStringIfTypePreservationFails()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "null2num", "33" },
                                    { "null2str", "bananas" },
                                    { "null2obj", @"{""x"": 1}" },
                                    { "null2arr", "[3, 2]" },
                                    { "bool2null", "null" },
                                    { "bool2num", "52" },
                                    { "num2null", "null" },
                                    { "num2bool", "true" },
                                    { "num2arr", "[1]" },
                                    { "str2bool", "false" }
                                },
                                "types-fall-back.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldExpandOctopusVariableReferences()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "Smtp.Host", "mail.example.com" },
                                    { "EmailSettings:SmtpHost", "#{Smtp.Host}" }
                                },
                                "appsettings.simple.json"),
                AssentConfiguration.Json);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-dos-bom.json"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-unix-nobom.json"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf16-dos-bom.json"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-windows1252-dos-nobom.json"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldUpgradeEncodingIfNecessaryToAccomodateVariables()
        {
            this.Assent(ReplaceToHex(new CalamariVariables{{"dagger", "\uFFE6"}}, "enc-windows1252-dos-nobom.json"),
                AssentConfiguration.Default);
        }
    }
}