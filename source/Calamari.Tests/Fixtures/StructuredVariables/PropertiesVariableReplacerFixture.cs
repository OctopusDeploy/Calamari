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
    public class PropertiesVariableReplacerFixture : VariableReplacerFixture
    {
        public PropertiesVariableReplacerFixture()
            : base(new PropertiesFormatVariableReplacer(CalamariPhysicalFileSystem.GetPhysicalFileSystem()))
        {
        }

        [Test]
        public void DoesNothingIfThereAreNoVariables()
        {
            var vars = new CalamariVariables();
            RunTest(vars, "example.properties");
        }

        [Test]
        public void HandlesAnEmptyFile()
        {
            var vars = new CalamariVariables();
            RunTest(vars, "blank.properties");
        }

        [Test]
        public void CanReplaceASimpleKeyValuePair()
        {
            var vars = new CalamariVariables
            {
                { "key1", "new-value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void CanReplaceAKeyThatContainsAPhysicalNewLine()
        {
            var vars = new CalamariVariables
            {
                { "key3", "new-value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void OnlyReplacesExistingKeys()
        {
            var vars = new CalamariVariables
            {
                { "no-such-key", "new-value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void EscapesSlashesInValues()
        {
            var vars = new CalamariVariables
            {
                { "key1", "new\\value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void EscapesDosNewLinesInValues()
        {
            var vars = new CalamariVariables
            {
                { "key1", "new\r\nvalue" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void EscapesNixNewLinesInValues()
        {
            var vars = new CalamariVariables
            {
                { "key1", "new\nvalue" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void EscapesTabsInValues()
        {
            var vars = new CalamariVariables
            {
                { "key1", "new\tvalue" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void EscapesALeadingSpaceInValues()
        {
            var vars = new CalamariVariables
            {
                { "key1", "  new value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void CanReplaceAValueInAKeyThatDoesntHaveAValue()
        {
            var vars = new CalamariVariables
            {
                { "key5", "  new value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void CanReplaceAValueInAKeyThatHasNeitherSeparatorNorValue()
        {
            var vars = new CalamariVariables
            {
                { "key6", "  new value" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void ShouldIgnoreOctopusPrefix()
        {
            var vars = new CalamariVariables
            {
                { "Octopus.Release.Id", "999" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void HandlesVariablesThatReferenceOtherVariables()
        {
            var vars = new CalamariVariables
            {
                { "key1", "#{a}" },
                { "a", "#{b}" },
                { "b", "new-value" },
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void UnicodeCharsInValuesAreEscaped()
        {
            var vars = new CalamariVariables
            {
                { "key1", "Θctopus" }
            };
            RunTest(vars, "example.properties");
        }

        [Test]
        public void UnicodeCharsInKeysAreHandled()
        {
            var vars = new CalamariVariables
            {
                { "kḛy", "new-value" }
            };
            RunTest(vars, "unicode-key.properties");
        }

        [Test]
        public void ShouldPreserveEncodingUtf8DosBom()
        {
            var vars = new CalamariVariables();
            RunHexTest(vars, "enc-utf8-dos-bom.properties");
        }

        [Test]
        public void ShouldPreserveEncodingUtf8UnixNoBom()
        {
            var vars = new CalamariVariables();
            RunHexTest(vars, "enc-utf8-unix-nobom.properties");
        }

        [Test]
        public void ShouldPreserveEncodingUtf16DosBom()
        {
            var vars = new CalamariVariables();
            RunHexTest(vars, "enc-utf16-dos-bom.properties");
        }

        [Test]
        public void ShouldPreserveEncodingWindows1252DosNoBom()
        {
            var vars = new CalamariVariables();
            RunHexTest(vars, "enc-windows1252-dos-nobom.properties");
        }

        void RunTest(CalamariVariables vars,
                     string file,
                     [CallerMemberName]
                     string testName = null,
                     [CallerFilePath]
                     string filePath = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            this.Assent(Replace(vars, file), TestEnvironment.AssentPropertiesConfiguration, testName, filePath);
        }

        void RunHexTest(CalamariVariables vars,
                     string file,
                     [CallerMemberName]
                     string testName = null,
                     [CallerFilePath]
                     string filePath = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            this.Assent(ReplaceToHex(vars, file), TestEnvironment.AssentConfiguration, testName, filePath);
        }
    }
}