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
        static KustomizeContainerImageReplacer CreateReplacer(string content = "", bool updateKustomizePatches = true)
        {
            var log = Substitute.For<ILog>();
            return new KustomizeContainerImageReplacer(content, "default-registry", updateKustomizePatches, log);
        }

        [TestFixture]
        public class DeterminePatchTypeFromFileTests
        {
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
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeTrue();
                }

                [Test]
                public void IsKustomizationResource_WithComponentApiVersionAndKind_ReturnsTrue()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1alpha1
kind: Component
resources:
- deployment.yaml";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeTrue();
                }

                [Test]
                public void IsKustomizationResource_WithDifferentApiVersion_ReturnsFalse()
                {
                    const string content = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_WithDifferentKind_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: CustomResource
metadata:
  name: test";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_EmptyContent_ReturnsFalse()
                {
                    var result = KustomizationValidator.IsKustomizationResource("");
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
                    var result = replacer.HasPatchesNode(content);
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
                    var result = replacer.HasPatchesNode(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasInlinePatches_WithEmptyPatchesField_ReturnsTrue()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches: []";
                    var replacer = CreateReplacer();
                    var result = replacer.HasPatchesNode(content);
                    result.Should().BeTrue();
                }

                [Test]
                public void HasInlinePatches_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var replacer = CreateReplacer();
                    var result = replacer.HasPatchesNode(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasInlinePatches_EmptyContent_ReturnsFalse()
                {
                    var replacer = CreateReplacer();
                    var result = replacer.HasPatchesNode("");
                    result.Should().BeFalse();
                }

                [Test]
                public void HasInlinePatches_NotAMappingNode_ReturnsFalse()
                {
                    const string content = @"- item1
- item2
- item3";
                    var replacer = CreateReplacer();
                    var result = replacer.HasPatchesNode(content);
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
                    var result = replacer.HasStrategicMergePatchNode(content);
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
                    var result = replacer.HasStrategicMergePatchNode(content);
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
                    var result = replacer.HasStrategicMergePatchNode(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasInlineStrategicMergePatches_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var replacer = CreateReplacer();
                    var result = replacer.HasStrategicMergePatchNode(content);
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
                    var result = replacer.HasJson6902PatchesNode(content);
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
                    var result = replacer.HasJson6902PatchesNode(content);
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
                    var result = replacer.HasJson6902PatchesNode(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasInlineJson6902Patches_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var replacer = CreateReplacer();
                    var result = replacer.HasJson6902PatchesNode(content);
                    result.Should().BeFalse();
                }
            }
        }
    }
}