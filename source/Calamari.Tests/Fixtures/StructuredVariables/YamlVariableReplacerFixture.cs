using System;
using System.Linq;
using System.Text.RegularExpressions;
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
        public YamlVariableReplacerFixture() : base((fs, log) => new YamlFormatVariableReplacer(fs, log))
        {
        }

        [Test]
        public void CommentReplacement()
        {
            var patch1 = @"
favourite:
  food: pear
";
            var patch2 = @"
favourite:
  food: bun
  color: blue
";
            this.Assent(Replace(new CalamariVariables{
            {
                "My.Variable.Alice", patch1
            },
            {
                "My.Variable.Bob", patch2
            }}, "comment-replacement.yaml"),
                AssentConfiguration.Yaml);
        }

        [Test]
        public void DoesNothingIfThereAreNoVariables()
        {
            this.Assent(Replace(new CalamariVariables(), "application.yaml"),
                AssentConfiguration.Yaml);
        }

        [Test]
        public void DoesNothingIfThereAreNoMatchingVariables()
        {
            Replace(new CalamariVariables { { "Non-matching", "variable" } }, "application.yaml");

            Log.MessagesInfoFormatted.Should().Contain(StructuredConfigMessages.NoStructuresFound);
            Log.MessagesVerboseFormatted.Should().NotContain(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*")));
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
                AssentConfiguration.Yaml);
            
            Log.MessagesVerboseFormatted.Count(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*"))).Should().Be(5);
            Log.MessagesVerboseFormatted.Should().Contain(StructuredConfigMessages.StructureFound("spring:h2:console:enabled"));
            Log.MessagesInfoFormatted.Should().NotContain(StructuredConfigMessages.NoStructuresFound);
        }

        [Test]
        public void ShouldMatchAndReplaceIgnoringCase()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "Spring:h2:Console:enabled", "false" }
                                },
                                "application.mixed-case.yaml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void CanReplaceSequenceWithString()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "server:ports", "none" }
                                },
                                "application.yaml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void ShouldReplaceVariablesInTopLevelSequence()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "1", "zwei" }
                                },
                                "application.top-level-sequence.yaml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void CanReplaceStructuresWithBlank()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "obj1", "" },
                                    { "seq1", null },
                                    { "seq2", "" }
                                },
                                "types.yaml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void ShouldReplaceWithColonInName()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "spring:h2:console:enabled", "false" }
                                },
                                "application.colon-in-name.yaml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void ShouldPreserveComments()
        {
            this.Assent(Replace(new CalamariVariables
                                {
                                    { "environment:matrix:2:DVersion", "stable" }
                                },
                                "comments.yml"),
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
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
                AssentConfiguration.Yaml);
        }

        [Test]
        public void ShouldPreserveMostCommonIndent()
        {
            this.Assent(Replace(new CalamariVariables { { "bbb:this:is", "much more" } },
                                "indenting.yaml"),
                AssentConfiguration.Yaml);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-dos-bom.yaml"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-unix-nobom.yaml"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf16-dos-bom.yaml"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-windows1252-dos-nobom.yaml"),
                AssentConfiguration.Default);
        }

        [Test]
        public void ShouldUpgradeEncodingIfNecessaryToAccomodateVariables()
        {
            this.Assent(ReplaceToHex(new CalamariVariables{{"dagger", "\uFFE6"}}, "enc-windows1252-dos-nobom.yaml"),
                AssentConfiguration.Default);
        }
    }
}