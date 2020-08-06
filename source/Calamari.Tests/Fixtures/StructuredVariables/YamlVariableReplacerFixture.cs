using System;
using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
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
                                    { "spring:datasource:url", "" },
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
        public void ShouldReplaceWithEmpty()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "obj1", "{}" },
                                    { "seq1", "[]" }
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

        [Test]
        public void OutputShouldBeStringEqualToVariableWhenInputTypeDoesNotMatch()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    // Note: although these replacements are unquoted in the output to match input style,
                                    // YAML strings do not require quotes, so they still string-equal to our variables.
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
                                "types-fall-back.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "spring:h2:console:enabled", "false" }
                                },
                                "application.colon-in-name.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldPreserveDirectives()
        {
            // Note: because YamlDotNet outputs the default directives alongside any custom ones, they have been
            // included in the input file here to avoid implying we require them to be added.
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "spring:h2:console:enabled", "true" }
                                },
                                "application.directives.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldPreserveComments()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "environment:matrix:2:DVersion", "stable" },
                                },
                                "comments.yml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ParsingErrorPositionIsReportedToUser()
        {
            var message = "";
            try
            {
                Replace(new CalamariVariables(), "broken.yaml");
            }
            catch (StructuredConfigFileParseException ex)
            {
                message = ex.Message;
            }

            message.Should().MatchRegex(@"(?i)\bLine\W+4\b");
            message.Should().MatchRegex(@"(?i)\bCol\w*\W+1\b");
        }

        [Test]
        public void ShouldPreserveFlowAndBlockStyles()
        {
            this.Assent(Replace(new CalamariVariables(),
                                "flow-and-block-styles.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void CanReplaceNestedList()
        {
            var nl = Environment.NewLine;
            this.Assent(Replace(new CalamariVariables
                                {
                                    {
                                        "pets-config:pets",
                                        $"- name: Susan{nl}  species: Cuttlefish{nl}- name: Craig{nl}  species: Manta Ray"
                                    }
                                },
                                "pets.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void CanReplaceStructures()
        {
            var nl = Environment.NewLine;
            this.Assent(Replace(new CalamariVariables
                                {
                                    {"mapping2mapping", $"this: is{nl}new: mapping"},
                                    {"sequence2sequence", $"- another{nl}- sequence{nl}- altogether"},
                                    {"mapping2sequence", $"- a{nl}- sequence"},
                                    {"sequence2mapping", $"  this: now{nl}  has:    keys"},
                                    {"mapping2string", "no longer a mapping"},
                                    {"sequence2string", "no longer a sequence"},
                                },
                                "structures.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldPreserveMostCommonIndent()
        {
            this.Assent(Replace(new CalamariVariables { { "bbb:this:is", "much more" } },
                                "indenting.yaml"),
                        TestEnvironment.AssentYamlConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-dos-bom.yaml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-unix-nobom.yaml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf16-dos-bom.yaml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-windows1252-dos-nobom.yaml"),
                        TestEnvironment.AssentConfiguration);
        }
    }
}