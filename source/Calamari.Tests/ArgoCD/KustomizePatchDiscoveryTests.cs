using System.IO;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.Common.Plumbing.FileSystem;
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
            fileSystem.FileExists(deploymentPatchPath).Returns(true);
            fileSystem.FileExists(servicePatchPath).Returns(true);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

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
            fileSystem.FileExists(deploymentPatchPath).Returns(true);
            fileSystem.FileExists(servicePatchPath).Returns(true);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

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

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

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
            fileSystem.FileExists(deploymentPatchPath).Returns(true);
            fileSystem.FileExists(servicePatchPath).Returns(true);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

            result.Should().HaveCount(3);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.StrategicMerge);
            result.Should().Contain(p => p.FilePath == servicePatchPath && p.Type == PatchType.Json6902);
            result.Should().Contain(p => p.FilePath == kustomizationPath && p.Type == PatchType.InlineJsonPatch);
        }

        [Test]
        public void DiscoverPatchFiles_WithNonExistentPatchFiles_IgnoresMissingFiles()
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
            fileSystem.FileExists(deploymentPatchPath).Returns(true);
            fileSystem.FileExists(missingPatchPath).Returns(false);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

            result.Should().HaveCount(1);
            result.Should().Contain(p => p.FilePath == deploymentPatchPath && p.Type == PatchType.StrategicMerge);
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

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

            result.Should().BeEmpty();
        }

        [Test]
        public void DiscoverPatchFiles_WithNonExistentKustomizationFile_ReturnsEmptyList()
        {
            fileSystem.FileExists(kustomizationPath).Returns(false);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

            result.Should().BeEmpty();
        }

        [Test]
        public void DiscoverPatchFiles_WithInvalidYaml_ReturnsEmptyList()
        {
            const string invalidYamlContent = @"invalid: yaml: content: [unclosed";

            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(invalidYamlContent);

            var result = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationPath, new InMemoryLog());

            result.Should().BeEmpty();
        }
    }
}