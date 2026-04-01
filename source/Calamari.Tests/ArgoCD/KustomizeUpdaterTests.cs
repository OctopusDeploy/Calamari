using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    /// <summary>
    /// Integration tests for KustomizeUpdater.Process() workflow.
    /// For unit tests of helper methods, see KustomizeContainerImageReplacerTests.
    /// </summary>
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

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
            tempDir = fileSystem.CreateTemporaryDirectory();

            Environment.SetEnvironmentVariable("OctopusEnabledFeatureToggles",
                OctopusFeatureToggles.KnownSlugs.KustomizePatchImageUpdatesFeatureToggle);
        }

        [TearDown]
        public void TearDown()
        {
            fileSystem?.DeleteDirectory(tempDir);

            Environment.SetEnvironmentVariable("OctopusEnabledFeatureToggles", null);
        }

        private void CreateKustomizationFile(string content)
        {
            var kustomizationPath = Path.Combine(tempDir, "kustomization.yaml");
            fileSystem.OverwriteFile(kustomizationPath, content);
        }

        private void CreatePatchFile(string fileName, string content)
        {
            var filePath = Path.Combine(tempDir, fileName);
            fileSystem.OverwriteFile(filePath, content);
        }

        [Test]
        public void Process_WithImagesFieldOnly_UpdatesImagesFieldOnly()
        {
            const string kustomizationContent = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.21
";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            CreateKustomizationFile(kustomizationContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedContent = fileSystem.ReadFile(Path.Combine(tempDir, "kustomization.yaml"));
            updatedContent.Should().Contain("1.25");
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

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("deployment-patch.yaml", patchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedPatchContent = fileSystem.ReadFile(Path.Combine(tempDir,"deployment-patch.yaml"));
            updatedPatchContent.Should().Contain("nginx:1.25");
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

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("deployment.json", patchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedPatchContent = fileSystem.ReadFile(Path.Combine(tempDir,"deployment.json"));
            updatedPatchContent.Should().Contain("nginx:1.25");
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

            CreateKustomizationFile(kustomizationContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedKustomizationContent = fileSystem.ReadFile(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("nginx:1.25");
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

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("deployment-patch.yaml", deploymentPatchContent);
            CreatePatchFile("service.json", servicePatchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");
            result.UpdatedImages.Should().Contain("busybox:stable");
            result.UpdatedImages.Count.Should().Be(2);

            var updatedDeploymentContent = fileSystem.ReadFile(Path.Combine(tempDir,"deployment-patch.yaml"));
            updatedDeploymentContent.Should().Contain("nginx:1.25");

            var updatedServiceContent = fileSystem.ReadFile(Path.Combine(tempDir,"service.json"));
            updatedServiceContent.Should().Contain("busybox:stable");

            var updatedKustomizationContent = fileSystem.ReadFile(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("busybox:stable");
        }

        [Test]
        public void Process_WithNoKustomizationFile_ReturnsEmptyResult()
        {
            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            // Don't create any files - the directory should be empty

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().BeEmpty();
            result.PatchedFiles.Should().BeEmpty();
        }

        [Test]
        public void Process_WithMixedInlineAndExternalPatches_UpdatesAllPatchTypes()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: ""1.21""
patchesStrategicMerge:
- |
  apiVersion: apps/v1
  kind: Deployment
  spec:
    template:
      spec:
        containers:
        - name: nginx
          image: nginx:1.21
- external-patch.yaml
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/1/image
      value: nginx:1.21
";

            const string externalPatchContent = @"
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: external-container
        image: nginx:1.21
";

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Kustomize,
                0);

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("external-patch.yaml", externalPatchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            // Check kustomization file was updated for images section and inline patches
            var updatedKustomizationContent = fileSystem.ReadFile(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("nginx:1.25");
            updatedKustomizationContent.Should().NotContain("nginx:1.21");

            // Check external patch file was also updated
            var updatedExternalPatchContent = fileSystem.ReadFile(Path.Combine(tempDir,"external-patch.yaml"));
            updatedExternalPatchContent.Should().Contain("nginx:1.25");
        }
    }
}
