using System.Collections.Generic;
using System.Linq;
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
    public class YamlJson6902PatchImageReplacerTests
    {
        readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
            new(ContainerImageReference.FromReferenceString("busybox:stable", "my-registry.com")),
        };

        ILog log;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
        }

        [Test]
        public void UpdateImages_WithReplaceOperation_UpdatesImageReference()
        {
            const string yamlContent = @"
- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.21
- op: add
  path: /metadata/labels/app
  value: test-app";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().NotContain("nginx:1.21");
        }

        [Test]
        public void UpdateImages_WithInitContainerReplaceOperation_UpdatesImageReference()
        {
            const string yamlContent = @"
- op: replace
  path: /spec/template/spec/initContainers/0/image
  value: nginx:1.21";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithAddContainersOperation_UpdatesImageReferences()
        {
            const string yamlContent = @"
- op: add
  path: /spec/template/spec/containers
  value:
  - name: nginx-container
    image: nginx:1.21
  - name: busybox-container
    image: my-registry.com/busybox:1.0";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("busybox:stable");
        }

        [Test]
        public void UpdateImages_WithAddInitContainersOperation_UpdatesImageReferences()
        {
            const string yamlContent = @"
- op: add
  path: /spec/template/spec/initContainers
  value:
  - name: init-nginx
    image: nginx:1.21
    command: ['sh', '-c', 'echo init']";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithNonImageOperations_DoesNotUpdate()
        {
            const string yamlContent = @"
- op: add
  path: /metadata/labels/app
  value: test-app
- op: replace
  path: /spec/replicas
  value: 3";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().BeEmpty();
            result.UpdatedContents.Should().Be(yamlContent);
        }

        [Test]
        public void UpdateImages_WithNonMatchingImages_DoesNotUpdate()
        {
            const string yamlContent = @"
- op: replace
  path: /spec/template/spec/containers/0/image
  value: redis:6.0
- op: add
  path: /spec/template/spec/containers/-
  value:
    name: postgres
    image: postgres:13";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().BeEmpty();
            result.UpdatedContents.Should().Be(yamlContent);
        }

        [Test]
        public void UpdateImages_WithMixedOperations_UpdatesOnlyMatchingImages()
        {
            const string yamlContent = @"
- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.21
- op: add
  path: /spec/template/spec/containers/-
  value:
    name: redis
    image: redis:6.0
- op: replace
  path: /spec/template/spec/initContainers/0/image
  value: my-registry.com/busybox:1.0";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
            result.UpdatedImageReferences.Should().HaveCount(2);
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("busybox:stable");
            result.UpdatedContents.Should().Contain("redis:6.0"); // Should remain unchanged
        }

        [Test]
        public void UpdateImages_WithEmptyContent_ReturnsNoChange()
        {
            var replacer = new YamlJson6902PatchImageReplacer("", ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().BeEmpty();
            result.UpdatedContents.Should().Be("");
        }

        [Test]
        public void UpdateImages_WithInvalidYaml_ReturnsNoChange()
        {
            const string invalidYaml = @"invalid: yaml: [unclosed";

            var replacer = new YamlJson6902PatchImageReplacer(invalidYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().BeEmpty();
            result.UpdatedContents.Should().Be(invalidYaml);
        }

        [Test]
        public void UpdateImages_WithComplexPatch_UpdatesCorrectly()
        {
            const string yamlContent = @"
- op: add
  path: /spec/template/spec/initContainers
  value:
  - name: init-permissions
    image: nginx:1.21
    command: ['sh', '-c', 'chmod 755 /shared']
    volumeMounts:
    - name: shared-data
      mountPath: /shared
- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.21
- op: add
  path: /spec/template/spec/containers/-
  value:
    name: sidecar
    image: nginx:1.21
    ports:
    - containerPort: 8080
- op: add
  path: /spec/template/spec/volumes
  value:
  - name: shared-data
    emptyDir: {}";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = replacer.UpdateImages(imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().NotContain("nginx:1.21");

            var updatedLines = result.UpdatedContents.Split('\n').Where(line => line.Contains("nginx:1.25")).ToArray();
            updatedLines.Should().HaveCount(3);
        }

        [Test]
        public void CombineResults_WithMultipleResults_MergesReplacementsAndUsesLatestContent()
        {
            var result1 = new ImageReplacementResult("content1", new HashSet<string> { "nginx:1.25" }, new HashSet<string>());
            var result2 = new ImageReplacementResult("content2", new HashSet<string> { "busybox:stable" }, new HashSet<string>());
            var result3 = new ImageReplacementResult("content3", new HashSet<string>(), new HashSet<string>());

            var combined = ImageReplacementResult.CombineResults(result1, result2, result3);

            combined.UpdatedContents.Should().Be("content3"); // Last non-empty content
            combined.UpdatedImageReferences.Should().HaveCount(2);
            combined.UpdatedImageReferences.Should().Contain("nginx:1.25");
            combined.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

        [Test]
        public void CombineResults_WithNoResults_ReturnsOriginalContent()
        {
            var combined = ImageReplacementResult.CombineResults();

            combined.UpdatedContents.Should().Be("");
            combined.UpdatedImageReferences.Should().BeEmpty();
        }


        [Test]
        public void ProcessContainersSequence_WithMultipleContainers_CombinesResults()
        {
            const string yamlContent = @"
- op: add
  path: /spec/template/spec/containers
  value:
    - name: nginx
      image: nginx:1.21
    - name: busybox
      image: my-registry.com/busybox:1.35";

            var replacer = new YamlJson6902PatchImageReplacer(yamlContent, ArgoCDConstants.DefaultContainerRegistry, log);
            var containersSequence = new YamlDotNet.RepresentationModel.YamlSequenceNode();
            var container1 = new YamlDotNet.RepresentationModel.YamlMappingNode();
            container1.Add("image", "nginx:1.21");
            var container2 = new YamlDotNet.RepresentationModel.YamlMappingNode();
            container2.Add("image", "my-registry.com/busybox:1.35");
            containersSequence.Add(container1);
            containersSequence.Add(container2);

            var result = replacer.ProcessContainersSequence(containersSequence, imagesToUpdate);

            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

    }
}