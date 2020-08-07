using System;
using System.Runtime.CompilerServices;
using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class XmlVariableReplacerFixture : VariableReplacerFixture
    {
        public XmlVariableReplacerFixture()
            : base(new XmlFormatVariableReplacer(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new InMemoryLog()))
        {
        }

        [Test]
        public void DoesNothingIfThereAreNoVariables()
        {
            var vars = new CalamariVariables();
            RunTest(vars, "complex.xml");
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
                { "b", "value-new" },
            };
            RunTest(vars, "elements.xml");
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-dos-bom.xml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf8-unix-nobom.xml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-utf16-dos-bom.xml"),
                        TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            this.Assent(ReplaceToHex(new CalamariVariables(), "enc-windows1252-dos-nobom.xml"),
                        TestEnvironment.AssentConfiguration);
        }

        void RunTest(CalamariVariables vars,
                     string file,
                     [CallerMemberName]
                     string testName = null,
                     [CallerFilePath]
                     string filePath = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            this.Assent(Replace(vars, file), TestEnvironment.AssentXmlConfiguration, testName, filePath);
        }
    }
}