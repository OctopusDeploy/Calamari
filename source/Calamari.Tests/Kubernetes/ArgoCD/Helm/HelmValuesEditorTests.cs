#if NET
using System;
using Calamari.ArgoCD.Helm;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Kubernetes.ArgoCD.Helm
{
    public class HelmValuesEditorTests
    {
        [Test]
        public void GenerateVariableDictionary_ReturnsDictionaryOfNodeValuesWithValues()
        {
            const string yamlContent = @"root:
  node1: ""node1value""
  node2:
     node2Nest:
         node2nestedValue: ""banana""
     node2Child1: ""node2child1value""
     node2Child2: 42
";

            var parser = new HelmYamlParser(yamlContent);

            var result = HelmValuesEditor.GenerateVariableDictionary(parser);

            var expected = new VariableDictionary
            {
                { "root.node1", "node1value" },
                { "root.node2.node2Child2", "42" },
                { "root.node2.node2Child1", "node2child1value" },
                { "root.node2.node2Nest.node2nestedValue", "banana" }
            };

            result.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void UpdateNodeValue_ReturnsModifiedYaml()
        {
            const string yamlContent = @"root:
  node1: ""node1value""
  node2:
     node2Nest:
         node2nestedValue: ""banana""
     node2Child1: ""node2child1value""
     node2Child2: 42
";
            var result = HelmValuesEditor.UpdateNodeValue(yamlContent, "root.node1", "awesome new value");

            const string expected = @"root:
  node1: ""awesome new value""
  node2:
     node2Nest:
         node2nestedValue: ""banana""
     node2Child1: ""node2child1value""
     node2Child2: 42
";
            //ensure platform-agnostic multiline comparison
            result.ReplaceLineEndings().Should().Be(expected.ReplaceLineEndings());
        }
    }
}
#endif