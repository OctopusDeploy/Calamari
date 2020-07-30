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
        public void CanReplaceAnElement()
        {
            var vars = new CalamariVariables
            {
                { "/document/setting[@id='id-1']", "value-new" }
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
        public void DoesNotThrowOnUnrecognisedNamespace()
        {
            var vars = new CalamariVariables
            {
                { "env:something", "value-new" }
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

        void RunTest(CalamariVariables vars, 
                     string file, 
                     [CallerMemberName] string testName = null,
                     [CallerFilePath] string filePath = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            this.Assent(Replace(vars, file), TestEnvironment.AssentXmlConfiguration, testName, filePath);
        }
    }
}