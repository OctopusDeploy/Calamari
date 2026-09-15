using System.Linq;
using Calamari.ArgoCD;
using FluentAssertions;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class YamlScalarSplicerTests
    {
        [Test]
        public void ReplaceValue_OnPlainScalar_LeavesTheRestOfTheFileUntouched()
        {
            const string document = "# top comment\r\nimage: nginx:1.21   # pinned\r\n\r\nother: keep\r\n";

            var result = Replace(document, "image", "nginx:1.25");

            result.Should().Be("# top comment\r\nimage: nginx:1.25   # pinned\r\n\r\nother: keep\r\n");
        }

        [Test]
        public void ReplaceValue_OnQuotedScalar_KeepsTheOriginalQuotes()
        {
            const string document = "image: \"nginx:1.21\"\nsingle: 'nginx:1.21'\n";

            var result = Replace(document, "image", "nginx:1.25");

            result.Should().Be("image: \"nginx:1.25\"\nsingle: 'nginx:1.21'\n");
        }

        [Test]
        public void ReplaceValue_OnLiteralBlock_ReindentsAndKeepsSurroundingText()
        {
            const string document = "patch: |-\r\n      kind: Deployment\r\n      image: nginx:1.21\r\ntarget: x\r\n";

            var result = Replace(document, "patch", "kind: Deployment\nimage: nginx:1.25");

            result.Should().Be("patch: |-\r\n      kind: Deployment\r\n      image: nginx:1.25\r\ntarget: x\r\n");
        }

        [Test]
        public void ReplaceValue_OnLiteralBlockWithBlankLine_DoesNotIndentTheBlankLine()
        {
            const string document = "patch: |\n  one\n\n  two\nafter: x\n";

            var result = Replace(document, "patch", "one\n\ntwo\n");

            result.Should().Be("patch: |\n  one\n\n  two\nafter: x\n");
        }

        [Test]
        public void ReplaceValue_OnLiteralBlockAtEndOfFileWithoutTrailingNewline_DoesNotAddOne()
        {
            const string document = "patch: |-\n  image: nginx:1.21";

            var result = Replace(document, "patch", "image: nginx:1.25");

            result.Should().Be("patch: |-\n  image: nginx:1.25");
        }

        [Test]
        public void ReplaceValues_WithSeveralEdits_AppliesThemAll()
        {
            const string document = "a: nginx:1.21\r\nb: nginx:1.21\r\nc: nginx:1.21\r\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var edits = new[] { "a", "b", "c" }
                .Select(key => new YamlScalarEdit((YamlScalarNode)root.Children[new YamlScalarNode(key)], "nginx:1.25"));

            var result = YamlScalarSplicer.ReplaceValues(document, edits);

            result.Should().Be("a: nginx:1.25\r\nb: nginx:1.25\r\nc: nginx:1.25\r\n");
        }

        static string Replace(string document, string key, string newValue)
        {
            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var node = (YamlScalarNode)root.Children[new YamlScalarNode(key)];
            return YamlScalarSplicer.ReplaceValue(document, node, newValue);
        }

        static YamlStream Load(string document)
        {
            return YamlStreamLoader.TryLoadSilent(document)!;
        }
    }
}
