using System;
using Calamari.ArgoCD.Helm;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.ArgoCD.Helm
{
    public class HelmValuesEditorTests
    {
        
    [Test]
    public void GenerateVariableDictionary_IgnoresIndexedMaps()
    {
        const string yamlContent = @"
images:
- nginx:
    value: docker.io/nginx
    version: 2.12
- cache:
    value: redis
    version: 1.98
";
        
        var parser = new HelmYamlParser(yamlContent);

        var result = HelmValuesEditor.GenerateVariableDictionary(parser);

        result.Should().BeEmpty();
    }
    
    [Test]
    public void GenerateVariableDictionary_IgnoresArrayStyleElements()
    {
        const string yamlContent = @"
image:
  pullPolicy: IfNotPresent
  repository: harrisonmeister/gitops-simple-app
  tag: ""1.0""

ingress:
  enabled: false
  annotations:
    {}
  hosts:
    - host: novasphere-dev.harrisonmeister
      paths: []
  tls: []
service:
  type: NodePort
  port: 8080
  nodePort: 30501
";

        var parser = new HelmYamlParser(yamlContent);

        var result = HelmValuesEditor.GenerateVariableDictionary(parser);

        var expected = new VariableDictionary
        {
            { "image.pullPolicy", "IfNotPresent" },
            { "image.repository", "harrisonmeister/gitops-simple-app" },
            { "image.tag", "1.0" },
            { "ingress.enabled", "false" },
            { "service.type", "NodePort" },
            { "service.port", "8080" },
            { "service.nodePort", "30501" },
        };

        result.Should().BeEquivalentTo(expected);
    }
        
        
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
