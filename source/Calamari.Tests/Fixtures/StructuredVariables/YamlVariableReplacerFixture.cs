﻿using System;
using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class YamlVariableReplacerFixture : VariableReplacerFixture
    {
        public YamlVariableReplacerFixture()
            : base(new YamlFormatVariableReplacer())
        {
        }

        [Test]
        public void CanReplaceStringWithString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "server:ports:0", "8080" },
                                    { "spring:h2:console:enabled", "false" },
                                    { "spring:loggers:1:name", "rolling-file" },
                                    { "environment", "production" }
                                },
                                "application.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldMatchAndReplaceIgnoringCase()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "Spring:h2:Console:enabled", "false" }
                                },
                                "application.mixed-case.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void CanReplaceMappingWithString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "server", "local" },
                                    { "spring:datasource", "none" }
                                },
                                "application.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void CanReplaceSequenceWithString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "server:ports", "none" }
                                },
                                "application.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void CanReplaceMappingDespiteReplacementInsideMapping()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "spring:datasource:dbcp2", "none" },
                                    { "spring:datasource", "none" }
                                },
                                "application.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void TypesAreInfluencedByThePositionInTheInputDocument()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "null1", "~" },
                                    { "null2", "null" },
                                    { "bool1", "true" },
                                    { "bool2", "no" },
                                    { "int1", "99" },
                                    { "float1", "1.99" },
                                    { "str1", "true" },
                                    { "str2", "~" },
                                    { "str3", "true" },
                                    { "str3", "aye" },
                                    { "str4", "cats.com" },
                                    { "obj1", $"fruit: apple{Environment.NewLine}animal: sloth" },
                                    { "seq1", $"- scissors{Environment.NewLine}- paper{Environment.NewLine}- rock" },
                                    { "seq2:0", "Orange" }
                                },
                                "types.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelSequence()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "1", "zwei" }
                                },
                                "application.top-level-sequence.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldReplaceWithNull()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    // Note: although these replacements are unquoted in the output to match input style,
                                    // YAML strings do not require quotes, so they still string-equal to our variables.
                                    { "bool1", "null" },
                                    { "int1", "~" },
                                    { "float1", "null" },
                                    { "str1", null },
                                    { "obj1", "~" },
                                    { "seq1", "null" },
                                    { "seq2:0", "~" }
                                },
                                "types.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "MyMessage", "Hello world!" },
                                    { "IThinkOctopusIsGreat", "Yes, I do!" },
                                    { "Octopus", "Foo: Bar" },
                                    { "OctopusRocks", "This is ignored" },
                                    { "Octopus.Rocks", "So is this" },
                                    { "Octopus:Section", "Should work" }
                                },
                                "application.ignore-octopus.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldExpandOctopusVariableReferences()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "Server.Port", "8080" },
                                    { "Server:Ports:0", "#{Server.Port}" }
                                },
                                "application.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }
    }
}