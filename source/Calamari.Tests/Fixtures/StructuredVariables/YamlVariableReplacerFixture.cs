using System;
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
                                    { "Spring:H2:Console:Enabled", "false" },
                                    { "environment", "production" }
                                },
                                "application.yaml"),
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
    }
}