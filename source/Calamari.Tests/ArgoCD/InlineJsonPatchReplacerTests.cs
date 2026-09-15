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
    public class InlineJsonPatchReplacerTests
    {
        readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
            new(ContainerImageReference.FromReferenceString("my-registry.com/busybox:stable", ArgoCDConstants.DefaultContainerRegistry)),
        };

        ILog log = new InMemoryLog();

        [Test]
        public void UpdateImages_WithAFoldedPatchThatHasNoRelevantImage_DoesNotWarn()
        {
            var inMemoryLog = new InMemoryLog();
            const string inputYaml = "patches:\n"
                                     + "  - patch: >\n"
                                     + "      some: folded prose that mentions no image at all\n"
                                     + "  - target:\n"
                                     + "      kind: Deployment\n"
                                     + "    patch: |-\n"
                                     + "      apiVersion: apps/v1\n"
                                     + "      kind: Deployment\n"
                                     + "      spec:\n"
                                     + "        template:\n"
                                     + "          spec:\n"
                                     + "            containers:\n"
                                     + "              - name: nginx\n"
                                     + "                image: nginx:1.21\n";

            var result = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, inMemoryLog).UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml.Replace("nginx:1.21", "nginx:1.25"));
            inMemoryLog.MessagesWarnFormatted.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithAFoldedPatchThatDoesHoldTheImage_WarnsAndLeavesItAlone()
        {
            var inMemoryLog = new InMemoryLog();
            const string inputYaml = "patches:\n"
                                     + "  - patch: >\n"
                                     + "      [{\"op\": \"replace\", \"path\": \"/spec/template/spec/containers/0/image\", \"value\": \"nginx:1.21\"}]\n";

            var result = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, inMemoryLog).UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
            inMemoryLog.MessagesWarnFormatted.Should().ContainMatch("*inline patch at line*Folded*");
        }

        [Test]
        public void UpdateImages_ChangesOnlyTheImageReference_PreservingCommentsAndLineEndings()
        {
            const string inputYaml = "# managed by the platform team\r\n"
                                     + "apiVersion: kustomize.config.k8s.io/v1beta1\r\n"
                                     + "kind: Kustomization\r\n"
                                     + "\r\n"
                                     + "patches:\r\n"
                                     + "  - target:\r\n"
                                     + "      kind: Deployment   # only the web tier\r\n"
                                     + "    patch: |-\r\n"
                                     + "      apiVersion: apps/v1\r\n"
                                     + "      kind: Deployment\r\n"
                                     + "      spec:\r\n"
                                     + "        template:\r\n"
                                     + "          spec:\r\n"
                                     + "            containers:\r\n"
                                     + "              - name: nginx\r\n"
                                     + "                image: nginx:1.21\r\n";

            var replacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be("nginx:1.25");
            result.UpdatedContents.Should().Be(inputYaml.Replace("nginx:1.21", "nginx:1.25"));
        }

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("my-registry.com/busybox:stable");
        }

        [Test]
        public void UpdateImages_WithNestedContainerStructures_UpdatesNestedImages()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: StatefulSet
    name: database-cluster
  patch: |-
    apiVersion: apps/v1
    kind: StatefulSet
    spec:
      template:
        spec:
          initContainers:
          - name: schema-migration
            image: nginx:1.21
          containers:
          - name: primary-db
            image: my-registry.com/busybox:1.0
          - name: sidecar-proxy
            image: proxy:v1.0
          volumes:
          - name: config-volume
            projected:
              sources:
              - configMap:
                  name: db-config
              - secret:
                  name: db-credentials
      volumeClaimTemplates:
      - metadata:
          name: data-volume
        spec:
          resources:
            requests:
              storage: 100Gi
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("my-registry.com/busybox:stable");
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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithEmptyContent_ReturnsNoChanges()
        {
            const string inputYaml = "";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithInvalidYaml_ReturnsNoChanges()
        {
            const string inputYaml = @"invalid: yaml: content: [unclosed";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

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

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithJson6902PatchReplaceOperation_UpdatesImageReference()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.21
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().NotContain("nginx:1.21");
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithJson6902PatchAddOperation_UpdatesImageReferences()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: add
      path: /spec/template/spec/containers
      value:
      - name: nginx
        image: nginx:1.21
      - name: busybox
        image: my-registry.com/busybox:1.0
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("my-registry.com/busybox:stable");
        }

        [Test]
        public void UpdateImages_WithMixedStrategicMergeAndJson6902Patches_UpdatesBothTypes()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: strategic-merge-deployment
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
    name: json6902-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: my-registry.com/busybox:1.0
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("my-registry.com/busybox:stable");
        }

        [Test]
        public void UpdateImages_WithJson6902PatchInitContainerOperation_UpdatesInitContainerImage()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: init-container-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/initContainers/0/image
      value: nginx:1.21
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Theory]
        [TestCase("docker.io/nginx:1.27.1", "docker.io/nginx:1.28.0")]
        [TestCase("nginx:1.27.1", "nginx:1.28.0")]
        [TestCase("us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v1",
                  "us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v2")]
        public void ReturnsSameImageBaseAsInYaml(string originalImage, string expectedImage)
        {
            var inputYaml = $@"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: my-deployment
  patch: |-
    apiVersion: apps/v1
    kind: Deployment
    spec:
      template:
        spec:
          containers:
          - name: my-container
            image: {originalImage}
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var update = new List<ContainerImageReferenceAndHelmReference>
            {
                new(ContainerImageReference.FromReferenceString(expectedImage, ArgoCDConstants.DefaultContainerRegistry))
            };

            var result = imageReplacer.UpdateImages(update);

            result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be(expectedImage);
            result.UpdatedContents.Should().Contain(expectedImage);
        }

        [Test]
        public void UpdateImages_WithJson6902PatchNonImageOperation_DoesNotUpdate()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: non-image-deployment
  patch: |-
    - op: replace
      path: /spec/replicas
      value: 3
    - op: add
      path: /metadata/labels/env
      value: production
";

            var imageReplacer = new InlineJsonPatchReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputYaml);
            result.UpdatedImageReferences.Should().BeEmpty();
        }
    }
}