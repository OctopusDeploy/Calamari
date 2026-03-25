using System;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class KustomizeContainerImageReplacerTests
    {
        static KustomizeContainerImageReplacer CreateReplacer(string content = "")
        {
            var log = Substitute.For<ILog>();
            return new KustomizeContainerImageReplacer(content, "default-registry", log);
        }
        [TestFixture]
        public class DeterminePatchTypeFromFileTests
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
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
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
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
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
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
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
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
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
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
                result.Should().BeNull();
            }

            [Test]
            public void DeterminePatchTypeFromFile_UnknownFormat_ReturnsNull()
            {
                const string content = @"some: unknown
format: that
doesnt: match
patterns: true";
                var replacer = CreateReplacer();
                var result = replacer.DeterminePatchTypeFromFile(content);
                result.Should().BeNull();
            }
        }

        [TestFixture]
        public class IsJson6902PatchContentTests
        {
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
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsJson6902PatchContent_ValidYamlArray_ReturnsTrue()
            {
                const string content = @"- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
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
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
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
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsJson6902PatchContent_MissingPathField_ReturnsFalse()
            {
                const string content = @"- op: replace
  value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_MissingOpField_ReturnsFalse()
            {
                const string content = @"- path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_InvalidOperation_ReturnsFalse()
            {
                const string content = @"- op: invalid_operation
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_NotAnArray_ReturnsFalse()
            {
                const string content = @"op: replace
path: /spec/template/spec/containers/0/image
value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_EmptyContent_ReturnsFalse()
            {
                var replacer = CreateReplacer();
                var result = replacer.IsJson6902PatchContent("");
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class IsStrategicMergePatchContentTests
        {
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
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithKind_ReturnsTrue()
            {
                const string content = @"kind: Service
metadata:
  name: my-service";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithMetadata_ReturnsTrue()
            {
                const string content = @"metadata:
  name: nginx-deployment
  labels:
    app: nginx";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
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
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithData_ReturnsTrue()
            {
                const string content = @"data:
  config.yaml: |
    setting: value";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
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
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
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
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_CaseInsensitive_ReturnsTrue()
            {
                const string content = @"APIVERSION: apps/v1
KIND: Deployment";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_JsonFormat_ReturnsTrue()
            {
                const string content = @"{
  ""apiVersion"": ""apps/v1"",
  ""kind"": ""Deployment""
}";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_NoKubernetesFields_ReturnsFalse()
            {
                const string content = @"some: random
yaml: content
without: kubernetes
fields: true";
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsStrategicMergePatchContent_EmptyContent_ReturnsFalse()
            {
                var replacer = CreateReplacer();
                var result = replacer.IsStrategicMergePatchContent("");
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class IsKustomizationResourceTests
        {
            [Test]
            public void IsKustomizationResource_WithKustomizeApiVersionAndKind_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationResource_WithComponentApiVersionAndKind_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1alpha1
kind: Component
resources:
- deployment.yaml";
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationResource_WithDifferentApiVersion_ReturnsFalse()
            {
                const string content = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx";
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationResource_WithDifferentKind_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: CustomResource
metadata:
  name: test";
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationResource_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationResource_EmptyContent_ReturnsFalse()
            {
                var replacer = CreateReplacer();
                var result = replacer.IsKustomizationResource("");
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class HasInlinePatchesTests
        {
            [Test]
            public void HasInlinePatches_WithPatchesField_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    spec:
      template:
        spec:
          containers:
          - name: nginx
            image: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches(content);
                result.Should().BeTrue();
            }

            [Test]
            public void HasInlinePatches_WithoutPatchesField_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_WithEmptyPatchesField_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches: []";
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches(content);
                result.Should().BeTrue();
            }

            [Test]
            public void HasInlinePatches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_EmptyContent_ReturnsFalse()
            {
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches("");
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_NotAMappingNode_ReturnsFalse()
            {
                const string content = @"- item1
- item2
- item3";
                var replacer = CreateReplacer();
                var result = replacer.HasInlinePatches(content);
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class HasInlineStrategicMergePatchesTests
        {
            [Test]
            public void HasInlineStrategicMergePatches_WithInlinePatches_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- |
  apiVersion: apps/v1
  kind: Deployment
  spec:
    template:
      spec:
        containers:
        - name: nginx
          image: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineStrategicMergePatches(content);
                result.Should().BeTrue();
            }

            [Test]
            public void HasInlineStrategicMergePatches_WithExternalFileReferences_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
- service-patch.yaml";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineStrategicMergePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineStrategicMergePatches_WithoutPatchesField_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineStrategicMergePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineStrategicMergePatches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineStrategicMergePatches(content);
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class HasInlineJson6902PatchesTests
        {
            [Test]
            public void HasInlineJson6902Patches_WithInlinePatches_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineJson6902Patches(content);
                result.Should().BeTrue();
            }

            [Test]
            public void HasInlineJson6902Patches_WithExternalFileReferences_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  path: deployment.json";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineJson6902Patches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineJson6902Patches_WithoutPatchesField_ReturnsFalse()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineJson6902Patches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineJson6902Patches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var replacer = CreateReplacer();
                var result = replacer.HasInlineJson6902Patches(content);
                result.Should().BeFalse();
            }
        }
    }
}