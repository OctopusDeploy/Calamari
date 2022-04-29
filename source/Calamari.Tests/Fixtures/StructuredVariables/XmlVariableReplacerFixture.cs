using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public class XmlVariableReplacerFixture : VariableReplacerFixture
    {
        public XmlVariableReplacerFixture() : base((fs, log) => new XmlFormatVariableReplacer(fs, log))
        {
        }

        [Test]
        public void DoesNothingIfThereAreNoVariables()
        {
            var vars = new CalamariVariables();
            RunTest(vars, "complex.xml");
        }

        [Test]
        public void DoesNothingIfThereAreNoMatchingVariables()
        {
            Replace(new CalamariVariables { { "Non-matching", "variable" } }, "complex.xml");

            Log.MessagesInfoFormatted.Should().Contain(StructuredConfigMessages.NoStructuresFound);
            Log.MessagesVerboseFormatted.Should().NotContain(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*")));
        }

        [Test]
        public void CanReplaceAComment()
        {
            var vars = new CalamariVariables
            {
                { "/document/comment()", "New Comment" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceAnAttribute()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-1']/@id", "id-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceATextNode()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-1']/text()", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceAnElementsText()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-1']", "value<new" }
            };

            RunTest(vars, "complex.xml");
            
            Log.MessagesVerboseFormatted.Count(m => Regex.IsMatch(m, StructuredConfigMessages.StructureFound(".*"))).Should().Be(1);
            Log.MessagesVerboseFormatted.Should().Contain(StructuredConfigMessages.StructureFound("/document/setting[@id='id-1']"));
            Log.MessagesInfoFormatted.Should().NotContain(StructuredConfigMessages.NoStructuresFound);
        }

        [Test]
        public void CanInsertTextIntoAnEmptyElement()
        {
            var vars = new CalamariVariables
            {
                { "/document/empty", "value<new" }
            };

            RunTest(vars, "elements.xml");
        }

        [Test]
        public void CanInsertTextIntoASelfClosingElement()
        {
            var vars = new CalamariVariables
            {
                { "/document/selfclosing", "value<new" }
            };

            RunTest(vars, "elements.xml");
        }

        [Test]
        public void CanReplaceAnElementsChildrenWhenTheElementHasMixedContent()
        {
            var vars = new CalamariVariables
            {
                { "/document/mixed", "<newElement />" }
            };

            RunTest(vars, "elements.xml");
        }

        [Test]
        public void CanReplaceAnElementsChildren()
        {
            var vars = new CalamariVariables
            {
                { "//moreSettings", "<a /><b />" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceAnElementsChildrenWhenTheNewChildrenHaveNamespaces()
        {
            var vars = new CalamariVariables
            {
                { "//moreSettings", "<db:a />" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void DoesNotModifyDocumentWhenVariableCannotBeParsedAsMarkup()
        {
            var vars = new CalamariVariables
            {
                { "//moreSettings", "<<<<" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void DoesntTreatVariableAsMarkupWhenReplacingAnElementThatContainsNoElementChildren()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-1']", "<a />" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceMultipleElements()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceMultipleElementsInDifferentPartsOfTheTree()
        {
            var vars = new CalamariVariables
            {
                { "//setting", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceCData()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-3']/text()", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceCDataInAnElement()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-3']", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanReplaceProcessingInstructions()
        {
            var vars = new CalamariVariables
            {
                { "/document/processing-instruction('xml-stylesheet')", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanInferNamespacePrefixesFromDocument()
        {
            var vars = new CalamariVariables
            {
                { "/document/unique:anotherSetting", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void CanUseXPath2Wildcards()
        {
            var vars = new CalamariVariables
            {
                { "//*:node", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void DoesNotThrowOnUnrecognisedNamespace()
        {
            var vars = new CalamariVariables
            {
                { "//env:something", "value-new" }
            };

            RunTest(vars, "complex.xml");
        }

        [Test]
        public void UsesTheFirstNamespaceWhenADuplicatePrefixIsFound()
        {
            var vars = new CalamariVariables
            {
                { "//parent/dupe:node", "value-new" }
            };

            RunTest(vars, "duplicate-prefixes.xml");
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            var vars = new CalamariVariables
            {
                { "Octopus.Release.Id", "999" }
            };
            RunTest(vars, "ignore-octopus.xml");
        }

        [Test]
        public void HandlesVariablesThatReferenceOtherVariables()
        {
            var vars = new CalamariVariables
            {
                { "/document/selfclosing", "#{a}" },
                { "a", "#{b}" },
                { "b", "value-new" }
            };
            RunTest(vars, "elements.xml");
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-dos-bom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-unix-nobom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf16-dos-bom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-windows1252-dos-nobom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldUpgradeEncodingIfNecessaryToAccomodateVariables()
        {
            this.Assent(ReplaceToHex(new CalamariVariables{{"/doc/dagger", "\uFFE6"}}, "enc-windows1252-dos-nobom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingIso88592UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables
                                     {
                                         { "//name[14]", "Miko\u0142aj" },
                                         { "//name[.='Micha\u0142']", "Micha\u0142+"}
                                     },
                                     "enc-iso88592-unix-nobom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        [Test]
        public void ShouldAdaptDeclaredEncodingWhenNecessary()
        {
            this.Assent(ReplaceToHex(new CalamariVariables { { "//name[14]", "\u5F20\u4F1F" } }, "enc-iso88592-unix-nobom.xml"),
                TestEnvironmentExtended.AssentConfiguration);
        }

        void RunTest(CalamariVariables vars,
                     string file,
                     [CallerMemberName]
                     string testName = null,
                     [CallerFilePath]
                     string filePath = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            this.Assent(Replace(vars, file), TestEnvironmentExtended.AssentXmlConfiguration, testName, filePath);
        }
    }
}