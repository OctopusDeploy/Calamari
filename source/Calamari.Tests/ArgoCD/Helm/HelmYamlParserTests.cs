#if NET
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

        [Test]
        public void UpdateNodeValue_WithNonDelimitedNodeValue_ReplacesValueInDocument()
        {
            const string yamlContent = @"
root:
  node1: 42
  node2: stable
";

            var sut = new HelmYamlParser(yamlContent);

            const string expectedUpdate = @"
root:
  node1: 69
  node2: stable
";

            var result = sut.UpdateContentForPath("root.node1", "69");

            //ensure platform-agnostic multiline comparison
            result.ReplaceLineEndings().Should().Be(expectedUpdate.ReplaceLineEndings());
        }

        [Test]
        public void UpdateNodeValue_WithDoubleQuoteDelimitedNodeValue_PreservesDelimitersWithNewValue()
        {
            const string yamlContent = @"
root:
  node1: 42
  node2: ""latest""
";

            var sut = new HelmYamlParser(yamlContent);

            const string expectedUpdate = @"
root:
  node1: 42
  node2: ""stable""
";

            var result = sut.UpdateContentForPath("root.node2", "stable");

            //ensure platform-agnostic multiline comparison
            result.ReplaceLineEndings().Should().Be(expectedUpdate.ReplaceLineEndings());
        }

        [Test]
        public void UpdateNodeValue_WithSingleQuoteDelimitedNodeValue_PreservesDelimitersWithNewValue()
        {
            const string yamlContent = @"
root:
  node1: 42
  node2: 'latest'
";

            var sut = new HelmYamlParser(yamlContent);

            const string expectedUpdate = @"
root:
  node1: 42
  node2: 'stable'
";

            var result = sut.UpdateContentForPath("root.node2", "stable");

            //ensure platform-agnostic multiline comparison
            result.ReplaceLineEndings().Should().Be(expectedUpdate.ReplaceLineEndings());
        }

        [Test]
        public void UpdateNodeValue_RespectsTrailingWhitespaceFromInput()
        {
            const string yamlContent = @"
root:
  node1: 42
  
";

            var sut = new HelmYamlParser(yamlContent);

            const string expectedUpdate = @"
root:
  node1: 69
  
";

            var result = sut.UpdateContentForPath("root.node1", "69");

            //ensure platform-agnostic multiline comparison
            result.ReplaceLineEndings().Should().Be(expectedUpdate.ReplaceLineEndings());
        }
    }
}

#endif