using System;
using Calamari.ArgoCD.Helm;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm
{
    public class HelmYamlParserTests
    {
        [Test]
        public void CreateDotPathsForNodes_FlattensYamlNodeToList()
        {
            const string yamlContent = @"
root:
  node1: ""node1value""
  node2:
     node2Nest:
         node2nestedValue: ""banana""
     node2Child1: ""node2child1value""
     node2Child2: 42
";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.CreateDotPathsForNodes();

            result.Count.Should().Be(4);
        }

        [Test]
        [TestCase("root.node1", "node1value")]
        [TestCase("root.node2.node2Child1", "node2child1value")]
        [TestCase("root.node2.node2Child2", "42")]
        [TestCase("root.node2.node2Nest.node2nestedValue", "banana")]
        public void GetValueAtPath_ReturnsTheValueOfTheSpecifiedNode(string path, string expected)
        {
            const string yamlContent = @"
root:
  node1: ""node1value""
  node2:
     node2Nest:
         node2nestedValue: banana
     node2Child1: ""node2child1value""
     node2Child2: 42
";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.GetValueAtPath(path);

            result.Should().Be(expected);
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void UpdateNodeValue_WithNonDelimitedNodeValue_ReplacesValueInDocument(string newLine)
        {
            var yamlContent = string.Join(newLine, "", "root:", "  node1: 42", "  node2: stable", "");

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node1", "69");

            result.Should().Be(string.Join(newLine, "", "root:", "  node1: 69", "  node2: stable", ""));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void UpdateNodeValue_WithDoubleQuoteDelimitedNodeValue_PreservesDelimitersWithNewValue(string newLine)
        {
            var yamlContent = string.Join(newLine, "", "root:", "  node1: 42", "  node2: \"latest\"", "");

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node2", "stable");

            result.Should().Be(string.Join(newLine, "", "root:", "  node1: 42", "  node2: \"stable\"", ""));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void UpdateNodeValue_WithSingleQuoteDelimitedNodeValue_PreservesDelimitersWithNewValue(string newLine)
        {
            var yamlContent = string.Join(newLine, "", "root:", "  node1: 42", "  node2: 'latest'", "");

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node2", "stable");

            result.Should().Be(string.Join(newLine, "", "root:", "  node1: 42", "  node2: 'stable'", ""));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void UpdateNodeValue_RespectsTrailingWhitespaceFromInput(string newLine)
        {
            var yamlContent = string.Join(newLine, "", "root:", "  node1: 42", "  ", "");

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node1", "69");

            result.Should().Be(string.Join(newLine, "", "root:", "  node1: 69", "  ", ""));
        }

        [Test]
        public void UpdateNodeValue_WithCrlfLineEndings_PreservesCrlfOnEveryLine()
        {
            const string yamlContent = "root:\r\n  node1: 42\r\n  node2: stable\r\n";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node1", "69");

            result.Should().Be("root:\r\n  node1: 69\r\n  node2: stable\r\n");
        }

        [Test]
        public void UpdateNodeValue_WithLfLineEndings_PreservesLfOnEveryLine()
        {
            const string yamlContent = "root:\n  node1: 42\n  node2: stable\n";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node1", "69");

            result.Should().Be("root:\n  node1: 69\n  node2: stable\n");
        }

        [Test]
        public void UpdateNodeValue_WithNoTrailingNewline_DoesNotAddOne()
        {
            const string yamlContent = "root:\n  node1: 42";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node1", "69");

            result.Should().Be("root:\n  node1: 69");
        }

        [Test]
        public void UpdateNodeValue_WithCrlfAndNoTrailingNewline_PreservesBoth()
        {
            const string yamlContent = "root:\r\n  node1: 42\r\n  node2: \"latest\"";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.node2", "stable");

            result.Should().Be("root:\r\n  node1: 42\r\n  node2: \"stable\"");
        }

        [Test]
        public void UpdateNodeValue_WithUnchangedPath_ReturnsContentByteForByte()
        {
            const string yamlContent = "root:\r\n  node1: 42\r\n";

            var sut = new HelmYamlParser(yamlContent);

            var result = sut.UpdateContentForPath("root.missing", "69");

            result.Should().Be(yamlContent);
        }

        [Test]
        public void CreateDotPathsForNodes_WithExistingDotNotationKeys_IgnoresThoseKeys()
        {
            const string yamlContent = @"
image.name: nginx
image.tag: latest
";
            
            var sut = new HelmYamlParser(yamlContent);

            var result = sut.CreateDotPathsForNodes();

            result.Should().BeEmpty();
        }
        
        [Test]
        public void CreateDotPathsForNodes_WithMixOfDotNotationAndNestedKeysKeys_ReturnsCorrectKeys()
        {
            const string yamlContent = @"
image1.name: nginx
image1.tag: latest
image2:
  name: alpine
  tag: 1.29
";
            
            var sut = new HelmYamlParser(yamlContent);

            var result = sut.CreateDotPathsForNodes();

            result.Should().BeEquivalentTo("image2.name", "image2.tag");
        }
        
    }
}

