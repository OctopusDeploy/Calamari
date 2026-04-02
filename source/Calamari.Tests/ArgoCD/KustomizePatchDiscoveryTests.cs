using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class KustomizePatchDiscoveryTests
    {
        ICalamariFileSystem fileSystem;
        string tempDir;
        string kustomizationPath;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            tempDir = Path.GetTempPath();
            kustomizationPath = Path.Combine(tempDir, "kustomization.yaml");
        }

        [Test]
        public void DiscoverPatchFiles_WithStrategicMergePatches_ReturnsCorrectPatchFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
- service-patch.yml
";

            var deploymentPatchPath = Path.Combine(tempDir, "deployment-patch.yaml");
            var servicePatchPath = Path.Combine(tempDir, "service-patch.yml");

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().HaveCount(2);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.StrategicMerge);
            result.Should().Contain(p => p.FilePath == servicePatchPath && p.Type == PatchType.StrategicMerge);
        }

        [Test]
        public void DiscoverPatchFiles_WithJson6902Patches_ReturnsCorrectPatchFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: my-deployment
  path: deployment.json
- target:
    kind: Service
    name: my-service
  path: service.json
";

            var deploymentPatchPath = Path.Combine(tempDir, "deployment.json");
            var servicePatchPath = Path.Combine(tempDir, "service.json");

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().HaveCount(2);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.Json6902);
            result.Should().Contain(p => p.FilePath == servicePatchPath && p.Type == PatchType.Json6902);
        }

        [Test]
        public void DiscoverPatchFiles_WithInlinePatches_ReturnsKustomizationFile()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches:
- target:
    kind: Deployment
    name: my-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.25
";

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().HaveCount(1);
            result.First().FilePath.Should().Be(kustomizationPath);
            result.First().Type.Should().Be(PatchType.InlineJsonPatch);
        }

        [Test]
        public void DiscoverPatchFiles_WithMixedPatchTypes_ReturnsAllPatchFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
patchesJson6902:
- target:
    kind: Service
    name: my-service
  path: service.json
patches:
- target:
    kind: Deployment
    name: my-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.25
";

            var deploymentPatchPath = Path.Combine(tempDir, "deployment-patch.yaml");
            var servicePatchPath = Path.Combine(tempDir, "service.json");

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().HaveCount(3);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.StrategicMerge);
            result.Should().Contain(p => p.FilePath == servicePatchPath && p.Type == PatchType.Json6902);
            result.Should().Contain(p => p.FilePath == kustomizationPath && p.Type == PatchType.InlineJsonPatch);
        }

        [Test]
        public void DiscoverPatchFiles_WithNonExistentPatchFiles_ReturnsAllDiscoveredPaths()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
- missing-patch.yaml
";

            var deploymentPatchPath = Path.Combine(tempDir, "deployment-patch.yaml");
            var missingPatchPath = Path.Combine(tempDir, "missing-patch.yaml");

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().HaveCount(2);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.StrategicMerge);
            result.Should().Contain(p => p.FilePath == missingPatchPath && p.Type == PatchType.StrategicMerge);
        }

        [Test]
        public void DiscoverPatchFiles_WithNoPatchFields_ReturnsEmptyList()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25
";

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(kustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().BeEmpty();
        }

        [Test]
        public void DiscoverPatchFiles_WithNonExistentKustomizationFile_ReturnsEmptyList()
        {
            fileSystem.FileExists(kustomizationPath).Returns(false);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().BeEmpty();
        }

        [Test]
        public void DiscoverPatchFiles_WithInvalidYaml_ReturnsEmptyList()
        {
            const string invalidYamlContent = @"invalid: yaml: content: [unclosed";

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(invalidYamlContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().BeEmpty();
        }

        [Test]
        public void DiscoverPatchFiles_WithNonKustomizationFile_ReturnsEmptyList()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var kustomizationPath = "/path/to/kustomization.yaml";

            // Valid YAML but not a kustomization file
            const string nonKustomizationContent = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: test-deployment
patches:
- deployment-patch.yaml";

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(nonKustomizationContent);

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, new InMemoryLog());
            var result = patchDiscovery.DiscoverPatch(kustomizationPath);

            result.Should().BeEmpty();
        }

        [TestFixture]
        public class InlinePatchDetectionTests
        {
            static ILog CreateMockLog() => Substitute.For<ILog>();

            [TestFixture]
            public class HasPatchesNodeTests
            {
                [Test]
                public void HasPatchesNode_WithPatchesField_ReturnsTrue()
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
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode(content, log);
                    result.Should().BeTrue();
                }

                [Test]
                public void HasPatchesNode_WithoutPatchesField_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasPatchesNode_WithEmptyPatchesField_ReturnsTrue()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches: []";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode(content, log);
                    result.Should().BeTrue();
                }

                [Test]
                public void HasPatchesNode_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasPatchesNode_EmptyContent_ReturnsFalse()
                {
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode("", log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasPatchesNode_NotAMappingNode_ReturnsFalse()
                {
                    const string content = @"- item1
- item2
- item3";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasPatchesNode(content, log);
                    result.Should().BeFalse();
                }
            }

            [TestFixture]
            public class HasStrategicMergePatchNodeTests
            {
                [Test]
                public void HasStrategicMergePatchNode_WithInlinePatches_ReturnsTrue()
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
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasStrategicMergePatchNode(content, log);
                    result.Should().BeTrue();
                }

                [Test]
                public void HasStrategicMergePatchNode_WithExternalFileReferences_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
- service-patch.yaml";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasStrategicMergePatchNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasStrategicMergePatchNode_WithoutPatchesField_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasStrategicMergePatchNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasStrategicMergePatchNode_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasStrategicMergePatchNode(content, log);
                    result.Should().BeFalse();
                }
            }

            [TestFixture]
            public class HasJson6902PatchesNodeTests
            {
                [Test]
                public void HasJson6902PatchesNode_WithInlinePatches_ReturnsTrue()
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
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasJson6902PatchesNode(content, log);
                    result.Should().BeTrue();
                }

                [Test]
                public void HasJson6902PatchesNode_WithExternalFileReferences_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  path: deployment.json";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasJson6902PatchesNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasJson6902PatchesNode_WithoutPatchesField_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasJson6902PatchesNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void HasJson6902PatchesNode_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.HasJson6902PatchesNode(content, log);
                    result.Should().BeFalse();
                }

                [Test]
                public void ProcessInlineStrategicMergePatches_WithInlinePatches_UpdatesImages()
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
          image: nginx:1.21";

                    var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
                    {
                        new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
                    };

                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.ProcessInlineStrategicMergePatches(content, imagesToUpdate, "default-registry", log);

                    result.UpdatedImageReferences.Should().Contain("nginx:1.25");
                    result.UpdatedContents.Should().Contain("nginx:1.25");
                }

                [Test]
                public void ProcessInlineStrategicMergePatches_WithNoMatches_ReturnsOriginalContent()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml";

                    var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
                    {
                        new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
                    };

                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.ProcessInlineStrategicMergePatches(content, imagesToUpdate, "default-registry", log);

                    result.UpdatedImageReferences.Should().BeEmpty();
                    result.UpdatedContents.Should().Be(content);
                }

                [Test]
                public void ProcessInlineJson6902Patches_WithInlinePatches_UpdatesImages()
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
      value: nginx:1.21";

                    var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
                    {
                        new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
                    };

                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.ProcessInlineJson6902Patches(content, imagesToUpdate, "default-registry", log);

                    result.UpdatedImageReferences.Should().Contain("nginx:1.25");
                    result.UpdatedContents.Should().Contain("nginx:1.25");
                }

                [Test]
                public void ProcessInlineJson6902Patches_WithNoMatches_ReturnsOriginalContent()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  path: deployment.json";

                    var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
                    {
                        new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
                    };

                    var log = CreateMockLog();
                    var result = KustomizePatchDiscovery.ProcessInlineJson6902Patches(content, imagesToUpdate, "default-registry", log);

                    result.UpdatedImageReferences.Should().BeEmpty();
                    result.UpdatedContents.Should().Be(content);
                }
            }
        }
    }
}