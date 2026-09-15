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

        [Test]
        public void ReplaceValues_WithTheSameNodeEditedTwice_AppliesItOnce()
        {
            // An alias makes YamlDotNet hand back the same node object, so callers can collect the
            // same edit twice. Splicing it twice would corrupt the document.
            const string document = "a: &x nginx:1.21\nb: *x\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var shared = (YamlScalarNode)root.Children[new YamlScalarNode("a")];
            var edits = new[] { new YamlScalarEdit(shared, "nginx:9"), new YamlScalarEdit(shared, "nginx:9") };

            var result = YamlScalarSplicer.ReplaceValues(document, edits);

            result.Should().Be("a: &x nginx:9\nb: *x\n");
        }

        [Test]
        public void CanReplaceValue_IsFalseForStylesThatCannotBeSpliced()
        {
            const string document = "a: >\n  folded\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

            YamlScalarSplicer.CanReplaceValue(document, node).Should().BeFalse();
        }

        [Test]
        public void CanReplaceValue_IsTrueForPlainQuotedAndLiteralScalars()
        {
            const string document = "a: plain\nb: \"quoted\"\nc: |\n  block\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;

            foreach (var key in new[] { "a", "b", "c" })
            {
                var node = (YamlScalarNode)root.Children[new YamlScalarNode(key)];
                YamlScalarSplicer.CanReplaceValue(document, node).Should().BeTrue($"{key} should be spliceable");
            }
        }

        [Test]
        public void ReplaceValue_OnBlockWithAnIndentIndicatorAndSurplusIndentation_KeepsTheSurplus()
        {
            // |2 declares two spaces of structure, so the remaining four on each line are part of the
            // value and must survive the replacement rather than being indented a second time.
            const string document = "a: |2\n      one\n      two\nb: keep\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

            node.Value.Should().Be("    one\n    two\n");
            YamlScalarSplicer.CanReplaceValue(document, node).Should().BeTrue();

            var result = YamlScalarSplicer.ReplaceValue(document, node, "    one\n    three\n");

            result.Should().Be("a: |2\n      one\n      three\nb: keep\n");
        }

        [Test]
        public void CanReplaceValue_IsTrueForBlockIndicatorsWhoseIndentationWeCanAccountFor()
        {
            foreach (var indicator in new[] { "|", "|-", "|+", "|2" })
            {
                const string indent = "  ";
                var document = $"a: {indicator}\n{indent}one\n{indent}two\nb: keep\n";

                var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
                var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

                YamlScalarSplicer.CanReplaceValue(document, node)
                                 .Should()
                                 .BeTrue($"{indicator} with matching indentation should be spliceable");
            }
        }

        [Test]
        public void ReplaceValue_ReplacingABlockValueWithItself_IsAByteForByteNoOp()
        {
            foreach (var indicator in new[] { "|", "|-", "|+" })
            foreach (var newLine in new[] { "\n", "\r\n" })
            {
                var document = $"a: {indicator}{newLine}  one{newLine}{newLine}  two{newLine}b: keep{newLine}";

                var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
                var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

                if (!YamlScalarSplicer.CanReplaceValue(document, node))
                    continue;

                YamlScalarSplicer.ReplaceValue(document, node, node.Value)
                                 .Should()
                                 .Be(document, $"{indicator} should round-trip unchanged");
            }
        }

        [Test]
        public void ReplaceValue_OnBlockWithLineEndingsDifferingFromTheFile_HarmonisesOnlyThatBlock()
        {
            // A CRLF file whose block content uses LF. The block is rewritten with the file's CRLF;
            // everything outside it stays byte-identical.
            const string document = "before: x\r\npatch: |-\r\n  kind: Deployment\n  image: nginx:1.21\r\nafter: y\r\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var node = (YamlScalarNode)root.Children[new YamlScalarNode("patch")];

            YamlScalarSplicer.CanReplaceValue(document, node).Should().BeTrue();

            var result = YamlScalarSplicer.ReplaceValue(document, node, "kind: Deployment\nimage: nginx:1.25");

            result.Should().Be("before: x\r\npatch: |-\r\n  kind: Deployment\r\n  image: nginx:1.25\r\nafter: y\r\n");
        }

        [Test]
        public void ReplaceValue_OnBlockWithAnIndentIndicatorAndMixedLineEndings_StillKeepsTheSurplus()
        {
            const string document = "a: |2\r\n      one\n      two\r\nb: keep\r\n";

            var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
            var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

            YamlScalarSplicer.CanReplaceValue(document, node).Should().BeTrue();

            var result = YamlScalarSplicer.ReplaceValue(document, node, node.Value);

            result.Should().Be("a: |2\r\n      one\r\n      two\r\nb: keep\r\n");
        }

        [Test]
        public void ReplaceValue_ReplacingAnIndentIndicatorBlockWithItself_IsAByteForByteNoOp()
        {
            foreach (var surplus in new[] { "", "  ", "    " })
            {
                var document = $"a: |2\n  {surplus}one\n  {surplus}two\nb: keep\n";

                var root = (YamlMappingNode)Load(document).Documents[0].RootNode;
                var node = (YamlScalarNode)root.Children[new YamlScalarNode("a")];

                YamlScalarSplicer.ReplaceValue(document, node, node.Value)
                                 .Should()
                                 .Be(document, $"surplus of {surplus.Length} spaces should round-trip");
            }
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
