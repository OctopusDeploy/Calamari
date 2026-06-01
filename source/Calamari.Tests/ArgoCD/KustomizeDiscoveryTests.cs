using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD;

public class KustomizeDiscoveryTests
{
    [Test]
    public void DeterminePatchTypeFromFile_JsonFile_WithJson6902Content_ReturnsJson6902()
    {
        const string content = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.25""
  }
]";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().Be(PatchType.Json6902);
    }

    [Test]
    public void DeterminePatchTypeFromFile_YamlFile_WithJson6902Content_ReturnsJson6902()
    {
        const string content = @"- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25
- op: add
  path: /metadata/annotations/updated
  value: true";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().Be(PatchType.Json6902);
    }

    [Test]
    public void DeterminePatchTypeFromFile_YamlFile_WithStrategicMergeContent_ReturnsStrategicMerge()
    {
        const string content = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().Be(PatchType.StrategicMerge);
    }

    [Test]
    public void DeterminePatchTypeFromFile_JsonFile_WithStrategicMergeContent_ReturnsStrategicMerge()
    {
        const string content = @"{
  ""apiVersion"": ""apps/v1"",
  ""kind"": ""Deployment"",
  ""spec"": {
    ""template"": {
      ""spec"": {
        ""containers"": [
          {
            ""name"": ""nginx"",
            ""image"": ""nginx:1.25""
          }
        ]
      }
    }
  }
}";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().Be(PatchType.StrategicMerge);
    }

    [Test]
    public void DeterminePatchTypeFromFile_KustomizationFile_ReturnsNull()
    {
        const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().BeNull();
    }

    [Test]
    public void DeterminePatchTypeFromFile_UnknownFormat_ReturnsNull()
    {
        const string content = @"some: unknown
format: that
doesnt: match
patterns: true";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.DeterminePatchType(content);
        result.Should().BeNull();
    }

    [Test]
    public void IsJson6902PatchContent_ValidJsonArray_ReturnsTrue()
    {
        const string content = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.25""
  }
]";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsJson6902PatchContent_ValidYamlArray_ReturnsTrue()
    {
        const string content = @"- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsJson6902PatchContent_MultipleOperations_ReturnsTrue()
    {
        const string content = @"- op: add
  path: /metadata/labels/updated
  value: 'true'
- op: remove
  path: /spec/replicas
- op: copy
  from: /spec/template
  path: /spec/backup";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsJson6902PatchContent_WithQuotedFieldNames_ReturnsTrue()
    {
        const string content = @"[
  {
    'op': 'test',
    'path': '/spec/replicas',
    'value': 3
  }
]";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsJson6902PatchContent_MissingPathField_ReturnsFalse()
    {
        const string content = @"- op: replace
  value: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeFalse();
    }

    [Test]
    public void IsJson6902PatchContent_MissingOpField_ReturnsFalse()
    {
        const string content = @"- path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeFalse();
    }

    [Test]
    public void IsJson6902PatchContent_InvalidOperation_ReturnsFalse()
    {
        const string content = @"- op: invalid_operation
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeFalse();
    }

    [Test]
    public void IsJson6902PatchContent_NotAnArray_ReturnsFalse()
    {
        const string content = @"op: replace
path: /spec/template/spec/containers/0/image
value: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent(content);
        result.Should().BeFalse();
    }

    [Test]
    public void IsJson6902PatchContent_EmptyContent_ReturnsFalse()
    {
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsJson6902PatchContent("");
        result.Should().BeFalse();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithApiVersion_ReturnsTrue()
    {
        const string content = @"apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithKind_ReturnsTrue()
    {
        const string content = @"kind: Service
metadata:
  name: my-service";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithMetadata_ReturnsTrue()
    {
        const string content = @"metadata:
  name: nginx-deployment
  labels:
    app: nginx";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithSpec_ReturnsTrue()
    {
        const string content = @"spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: nginx";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithData_ReturnsTrue()
    {
        const string content = @"data:
  config.yaml: |
    setting: value";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithImageField_ReturnsTrue()
    {
        const string content = @"spec:
  template:
    spec:
      containers:
      - image: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_WithContainersField_ReturnsTrue()
    {
        const string content = @"spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_CaseInsensitive_ReturnsTrue()
    {
        const string content = @"APIVERSION: apps/v1
KIND: Deployment";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_JsonFormat_ReturnsTrue()
    {
        const string content = @"{
  ""apiVersion"": ""apps/v1"",
  ""kind"": ""Deployment""
}";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeTrue();
    }

    [Test]
    public void IsStrategicMergePatchContent_NoKubernetesFields_ReturnsFalse()
    {
        const string content = @"some: random
yaml: content
without: kubernetes
fields: true";
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent(content);
        result.Should().BeFalse();
    }

    [Test]
    public void IsStrategicMergePatchContent_EmptyContent_ReturnsFalse()
    {
        var discovery = new KustomizeDiscovery(new InMemoryLog());
        var result = discovery.IsStrategicMergePatchContent("");
        result.Should().BeFalse();
    }
}