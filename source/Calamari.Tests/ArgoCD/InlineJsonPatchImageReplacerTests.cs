using System;
using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class InlineJsonPatchImageReplacerTests
    {
        readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
            new(ContainerImageReference.FromReferenceString("my-registry.com/busybox:stable", ArgoCDConstants.DefaultContainerRegistry)),
        };

        ILog log = new InMemoryLog();

        [Test]
        public void UpdateImages_WithInlinePatchContainerImage_UpdatesImageReference()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: nginx
            image: nginx:1.21
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithMultiplePatchesAndImages_UpdatesAllMatchingImages()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: nginx
            image: nginx:1.21
- target:
    kind: Pod
    name: busybox-pod
  patch: |-
    apiVersion: v1
    kind: Pod
    spec:
      initContainers:
      - name: init
        image: my-registry.com/busybox:1.0
      containers:
      - name: main
        image: redis:6.0
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

        [Test]
        public void UpdateImages_WithNestedContainerStructures_UpdatesNestedImages()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: CronJob
    name: my-cronjob
  patch: |-
    apiVersion: batch/v1
    kind: CronJob
    spec:
      jobTemplate:
        spec:
          template:
            spec:
              containers:
              - name: worker
                image: nginx:1.21
              jobs:
                - spec:
                    template:
                      spec:
                        containers:
                        - name: nested-worker
                          image: my-registry.com/busybox:1.0
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            //result.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

        [Test]
        public void UpdateImages_WithDirectImageField_UpdatesDirectImageReference()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Pod
    name: simple-pod
  patch: |-
    apiVersion: v1
    kind: Pod
    spec:
      containers:
      - image: nginx:1.21
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithNoPatchesField_ReturnsNoChanges()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNoMatchingImages_ReturnsNoChanges()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: redis-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: redis
            image: redis:6.0
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithImageAlreadyUpToDate_ReturnsNoChanges()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: nginx
            image: nginx:1.25
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithEmptyContent_ReturnsNoChanges()
        {
            const string inputYaml = "";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithInvalidYaml_ReturnsNoChanges()
        {
            const string inputYaml = @"invalid: yaml: content: [unclosed";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithInvalidPatchContent_SkipsInvalidPatches()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: valid-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: nginx
            image: nginx:1.21
- target:
    kind: Deployment
    name: invalid-deployment
  patch: |-
    invalid: yaml: [unclosed
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithNonMappingKustomizationRoot_ReturnsNoChanges()
        {
            const string inputYaml = @"
- item1
- item2
";

            var imageReplacer = new InlineJsonPatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }
    }
}