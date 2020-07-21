﻿using System;
using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
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
                                "appsettings.simple.json"),
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
                                "appsettings.ignore-octopus.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "0:Property", "NewValue" }
                                },
                                "appsettings.top-level-array.json"),
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
                                "appsettings.existing-expected.json"),
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
                                "appsettings.simple.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithAnEmptyString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "" }
                                },
                                "appsettings.single.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithNull()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", null }
                                },
                                "appsettings.single.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EnvironmentVariables:Hosting:Environment", "Production" }
                                },
                                "appsettings.colon-in-name.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceWholeObject()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"{""To"": ""rob@octopus.com"", ""Cc"": ""henrik@octopus.com""}" }
                                },
                                "appsettings.simple.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1", "henrik@octopus.com" }
                                },
                                "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplacePropertyOfAnElementInArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients:1:Email", "henrik@octopus.com" }
                                },
                                "appsettings.object-array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceEntireArray()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:DefaultRecipients", @"[""mike@octopus.com"", ""henrik@octopus.com""]" }
                                },
                                "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceNumber()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:SmtpPort", "8023" }
                                },
                                "appsettings.array.json"),
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
                                "appsettings.decimals.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldReplaceBoolean()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "EmailSettings:UseProxy", "true" }
                                },
                                "appsettings.array.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }

        [Test]
        public void ShouldFallBackToStringIfTypePreservationFails()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "null2num", "33" },
                                    { "null2obj", @"{""x"": 1}" },
                                    { "bool2num", "52" },
                                    { "num2null", "null" },
                                    { "num2bool", "true" },
                                    { "num2arr", "[1]" },
                                    { "str2bool", "false" }
                                },
                                "types-fall-back.json"),
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
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
                        TestEnvironment.AssentJsonDeepCompareConfiguration);
        }
    }
}