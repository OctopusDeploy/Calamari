using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
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
    public class KustomizeUpdaterTests
    {
        readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
            new(ContainerImageReference.FromReferenceString("busybox:stable", "my-registry.com")),
        };

        ILog log;
        ICalamariFileSystem fileSystem;
        string tempDir;
        string kustomizationPath;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            fileSystem = Substitute.For<ICalamariFileSystem>();
            tempDir = "/tmp";
            
            var subDirPath = Path.Combine(tempDir, ".");
            kustomizationPath = Path.Combine(subDirPath, "kustomization.yaml");
            
            fileSystem.DirectoryExists(tempDir).Returns(true);
        }

        private void SetupKustomizationFile(string content)
        {
            var subDirPath = Path.Combine(tempDir, ".");
            fileSystem.DirectoryExists(subDirPath).Returns(true);
            
            fileSystem.FileExists(kustomizationPath).Returns(true);
            fileSystem.ReadFile(kustomizationPath).Returns(content);
            
            var altPath1 = Path.Combine(subDirPath, "kustomization.yml");
            var altPath2 = Path.Combine(subDirPath, "Kustomization");
            fileSystem.FileExists(altPath1).Returns(false);
            fileSystem.FileExists(altPath2).Returns(false);
        }

        [Test]
        public void Process_WithImagesFieldOnly_UpdatesImagesFieldOnly()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.21
";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            SetupKustomizationFile(kustomizationContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(Arg.Any<string>(), Arg.Is<string>(content => content.Contains("\"1.25\"")));
        }

        [Test]
        public void Process_WithStrategicMergePatch_UpdatesPatchFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
";

            const string patchContent = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            var subDirPath = Path.Combine(tempDir, ".");
            var patchPath = Path.Combine(subDirPath, "deployment-patch.yaml");

            SetupKustomizationFile(kustomizationContent);
            fileSystem.FileExists(patchPath).Returns(true);
            fileSystem.ReadFile(patchPath).Returns(patchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(patchPath, Arg.Is<string>(content => content.Contains("nginx:1.25")));
        }

        [Test]
        public void Process_WithJson6902Patch_UpdatesPatchFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  path: deployment.json
";

            const string patchContent = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.21""
  }
]";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            var subDirPath = Path.Combine(tempDir, ".");
            var patchPath = Path.Combine(subDirPath, "deployment.json");

            SetupKustomizationFile(kustomizationContent);
            fileSystem.FileExists(patchPath).Returns(true);
            fileSystem.ReadFile(patchPath).Returns(patchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(patchPath, Arg.Is<string>(content => content.Contains("nginx:1.25")));
        }

        [Test]
        public void Process_WithInlinePatches_UpdatesKustomizationFile()
        {
            const string kustomizationContent = @"
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

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            SetupKustomizationFile(kustomizationContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(kustomizationPath, Arg.Is<string>(content => content.Contains("nginx:1.25")));
        }

        [Test]
        public void Process_WithMixedPatchTypes_UpdatesAllFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: redis
  newTag: 6.0
patchesStrategicMerge:
- deployment-patch.yaml
patchesJson6902:
- target:
    kind: Service
    name: my-service
  path: service.json
patches:
- target:
    kind: Pod
    name: my-pod
  patch: |-
    apiVersion: v1
    kind: Pod
    spec:
      containers:
      - name: busybox
        image: my-registry.com/busybox:1.0
";

            const string deploymentPatchContent = @"
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
";

            const string servicePatchContent = @"[
  {
    ""op"": ""add"",
    ""path"": ""/metadata/annotations/image"",
    ""value"": ""my-registry.com/busybox:1.0""
  }
]";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            var subDirPath = Path.Combine(tempDir, ".");
            var deploymentPatchPath = Path.Combine(subDirPath, "deployment-patch.yaml");
            var servicePatchPath = Path.Combine(subDirPath, "service.json");

            SetupKustomizationFile(kustomizationContent);
            fileSystem.FileExists(deploymentPatchPath).Returns(true);
            fileSystem.ReadFile(deploymentPatchPath).Returns(deploymentPatchContent);
            fileSystem.FileExists(servicePatchPath).Returns(true);
            fileSystem.ReadFile(servicePatchPath).Returns(servicePatchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            result.UpdatedImages.Should().Contain("busybox:stable");
            result.UpdatedImages.Count.Should().Be(2); // nginx and busybox (duplicates are removed from the set)

            fileSystem.Received(1).OverwriteFile(deploymentPatchPath, Arg.Is<string>(content => content.Contains("nginx:1.25")));
            fileSystem.Received(1).OverwriteFile(servicePatchPath, Arg.Is<string>(content => content.Contains("busybox:stable")));
            fileSystem.Received(1).OverwriteFile(kustomizationPath, Arg.Is<string>(content => content.Contains("busybox:stable")));
        }

        [Test]
        public void Process_WithNonExistentPatchFiles_SkipsMissingFiles()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- deployment-patch.yaml
- missing-patch.yaml
";

            const string patchContent = @"
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            var subDirPath = Path.Combine(tempDir, ".");
            var existingPatchPath = Path.Combine(subDirPath, "deployment-patch.yaml");
            var missingPatchPath = Path.Combine(subDirPath, "missing-patch.yaml");

            SetupKustomizationFile(kustomizationContent);
            fileSystem.FileExists(existingPatchPath).Returns(true);
            fileSystem.ReadFile(existingPatchPath).Returns(patchContent);
            fileSystem.FileExists(missingPatchPath).Returns(false);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(existingPatchPath, Arg.Any<string>());
            fileSystem.DidNotReceive().OverwriteFile(missingPatchPath, Arg.Any<string>());
        }

        [Test]
        public void Process_WithNoKustomizationFile_ReturnsEmptyResult()
        {
            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            fileSystem.FileExists(Arg.Any<string>()).Returns(false);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().BeEmpty();
            result.PatchedFileContent.Should().BeEmpty();
        }

        [Test]
        public void Process_WithInvalidPatchFile_SkipsInvalidPatchAndContinues()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- valid-patch.yaml
- invalid-patch.yaml
";

            const string validPatchContent = @"
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
";

            const string invalidPatchContent = @"invalid: yaml: [unclosed";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            var subDirPath = Path.Combine(tempDir, ".");
            var validPatchPath = Path.Combine(subDirPath, "valid-patch.yaml");
            var invalidPatchPath = Path.Combine(subDirPath, "invalid-patch.yaml");

            SetupKustomizationFile(kustomizationContent);
            fileSystem.FileExists(validPatchPath).Returns(true);
            fileSystem.ReadFile(validPatchPath).Returns(validPatchContent);
            fileSystem.FileExists(invalidPatchPath).Returns(true);
            fileSystem.ReadFile(invalidPatchPath).Returns(invalidPatchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            fileSystem.Received(1).OverwriteFile(validPatchPath, Arg.Any<string>());
            // Invalid patch should not be updated
            fileSystem.DidNotReceive().OverwriteFile(invalidPatchPath, Arg.Any<string>());
        }
    }
}