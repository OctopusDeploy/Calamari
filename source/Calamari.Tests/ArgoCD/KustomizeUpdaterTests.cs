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
using Calamari.Tests.Fixtures.Integration.FileSystem;
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

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
            tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        private void CreateKustomizationFile(string content)
        {
            var kustomizationPath = Path.Combine(tempDir, "kustomization.yaml");
            File.WriteAllText(kustomizationPath, content);
        }

        private void CreatePatchFile(string fileName, string content)
        {
            var filePath = Path.Combine(tempDir, fileName);
            File.WriteAllText(filePath, content);
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

            var updatedContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
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

            var updatedPatchContent = File.ReadAllText(Path.Combine(tempDir, "deployment-patch.yaml"));
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

            var updatedPatchContent = File.ReadAllText(Path.Combine(tempDir, "deployment.json"));
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

            var updatedKustomizationContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
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

            var updatedDeploymentContent = File.ReadAllText(Path.Combine(tempDir, "deployment-patch.yaml"));
            updatedDeploymentContent.Should().Contain("nginx:1.25");

            var updatedServiceContent = File.ReadAllText(Path.Combine(tempDir, "service.json"));
            updatedServiceContent.Should().Contain("busybox:stable");

            var updatedKustomizationContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("busybox:stable");
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

            var existingPatchPath = Path.Combine(tempDir, "deployment-patch.yaml");
            var missingPatchPath = Path.Combine(tempDir, "missing-patch.yaml");

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("deployment-patch.yaml", patchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedPatchContent = File.ReadAllText(Path.Combine(tempDir, "deployment-patch.yaml"));
            updatedPatchContent.Should().Contain("nginx:1.25");

            File.Exists(Path.Combine(tempDir, "missing-patch.yaml")).Should().BeFalse();
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

            var validPatchPath = Path.Combine(tempDir, "valid-patch.yaml");
            var invalidPatchPath = Path.Combine(tempDir, "invalid-patch.yaml");

            CreateKustomizationFile(kustomizationContent);
            CreatePatchFile("valid-patch.yaml", validPatchContent);
            CreatePatchFile("invalid-patch.yaml", invalidPatchContent);

            var updater = new KustomizeUpdater(imagesToUpdate, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().Contain("nginx:1.25");

            var updatedValidPatchContent = File.ReadAllText(Path.Combine(tempDir, "valid-patch.yaml"));
            updatedValidPatchContent.Should().Contain("nginx:1.25");

            var unchangedInvalidPatchContent = File.ReadAllText(Path.Combine(tempDir, "invalid-patch.yaml"));
            unchangedInvalidPatchContent.Should().Be(invalidPatchContent);
        }

        [Test]
        public void Process_WithInlineStrategicMergePatches_UpdatesKustomizationFile()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- |
  apiVersion: apps/v1
  kind: Deployment
  metadata:
    name: nginx-deployment
  spec:
    template:
      spec:
        initContainers:
        - name: init-setup
          image: nginx:1.21
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

            var updatedKustomizationContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("nginx:1.25");
            // Should update both init containers and regular containers
            updatedKustomizationContent.Should().NotContain("nginx:1.21");
        }

        [Test]
        public void Process_WithInlineJson6902Patches_UpdatesKustomizationFile()
        {
            const string kustomizationContent = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.21
    - op: add
      path: /spec/template/spec/initContainers
      value:
      - name: init-container
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

            var updatedKustomizationContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("nginx:1.25");
            // Should update both replace and add operations
            updatedKustomizationContent.Should().NotContain("nginx:1.21");
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
            var updatedKustomizationContent = File.ReadAllText(Path.Combine(tempDir, "kustomization.yaml"));
            updatedKustomizationContent.Should().Contain("nginx:1.25");
            updatedKustomizationContent.Should().NotContain("nginx:1.21");

            // Check external patch file was also updated
            var updatedExternalPatchContent = File.ReadAllText(Path.Combine(tempDir, "external-patch.yaml"));
            updatedExternalPatchContent.Should().Contain("nginx:1.25");
        }
    }

    [TestFixture]
    public class KustomizeUpdaterHelperMethodsTests
    {
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
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "patch.json");
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
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "patch.yaml");
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
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "patch.yaml");
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
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "patch.json");
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
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "kustomization.yaml");
                result.Should().BeNull();
            }

            [Test]
            public void DeterminePatchTypeFromFile_UnknownFormat_ReturnsNull()
            {
                const string content = @"some: unknown
format: that
doesnt: match
patterns: true";
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "unknown.yaml");
                result.Should().BeNull();
            }

            [Test]
            public void DeterminePatchTypeFromFile_NonYamlJsonExtension_ReturnsNull()
            {
                const string content = @"some content";
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "file.txt");
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
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsJson6902PatchContent_ValidYamlArray_ReturnsTrue()
            {
                const string content = @"- op: replace
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
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
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
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
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsJson6902PatchContent_MissingPathField_ReturnsFalse()
            {
                const string content = @"- op: replace
  value: nginx:1.25";
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_MissingOpField_ReturnsFalse()
            {
                const string content = @"- path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_InvalidOperation_ReturnsFalse()
            {
                const string content = @"- op: invalid_operation
  path: /spec/template/spec/containers/0/image
  value: nginx:1.25";
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_NotAnArray_ReturnsFalse()
            {
                const string content = @"op: replace
path: /spec/template/spec/containers/0/image
value: nginx:1.25";
                var result = KustomizeUpdater.IsJson6902PatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsJson6902PatchContent_EmptyContent_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsJson6902PatchContent("");
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
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithKind_ReturnsTrue()
            {
                const string content = @"kind: Service
metadata:
  name: my-service";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithMetadata_ReturnsTrue()
            {
                const string content = @"metadata:
  name: nginx-deployment
  labels:
    app: nginx";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
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
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_WithData_ReturnsTrue()
            {
                const string content = @"data:
  config.yaml: |
    setting: value";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
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
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
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
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_CaseInsensitive_ReturnsTrue()
            {
                const string content = @"APIVERSION: apps/v1
KIND: Deployment";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_JsonFormat_ReturnsTrue()
            {
                const string content = @"{
  ""apiVersion"": ""apps/v1"",
  ""kind"": ""Deployment""
}";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeTrue();
            }

            [Test]
            public void IsStrategicMergePatchContent_NoKubernetesFields_ReturnsFalse()
            {
                const string content = @"some: random
yaml: content
without: kubernetes
fields: true";
                var result = KustomizeUpdater.IsStrategicMergePatchContent(content);
                result.Should().BeFalse();
            }

            [Test]
            public void IsStrategicMergePatchContent_EmptyContent_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsStrategicMergePatchContent("");
                result.Should().BeFalse();
            }
        }

        [TestFixture]
        public class IsKustomizationFileTests
        {
            [Test]
            public void IsKustomizationFile_KustomizationYaml_ReturnsTrue()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/kustomization.yaml");
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationFile_KustomizationYml_ReturnsTrue()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/kustomization.yml");
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationFile_CaseInsensitive_ReturnsTrue()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/KUSTOMIZATION.YAML");
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationFile_MixedCase_ReturnsTrue()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/Kustomization.Yml");
                result.Should().BeTrue();
            }

            [Test]
            public void IsKustomizationFile_OtherYamlFile_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/deployment.yaml");
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationFile_JsonFile_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/kustomization.json");
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationFile_NoExtension_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsKustomizationFile("/path/to/kustomization");
                result.Should().BeFalse();
            }

            [Test]
            public void IsKustomizationFile_EmptyPath_ReturnsFalse()
            {
                var result = KustomizeUpdater.IsKustomizationFile("");
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
                var result = KustomizeUpdater.HasInlinePatches(content);
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
                var result = KustomizeUpdater.HasInlinePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_WithEmptyPatchesField_ReturnsTrue()
            {
                const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patches: []";
                var result = KustomizeUpdater.HasInlinePatches(content);
                result.Should().BeTrue();
            }

            [Test]
            public void HasInlinePatches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var result = KustomizeUpdater.HasInlinePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_EmptyContent_ReturnsFalse()
            {
                var result = KustomizeUpdater.HasInlinePatches("");
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlinePatches_NotAMappingNode_ReturnsFalse()
            {
                const string content = @"- item1
- item2
- item3";
                var result = KustomizeUpdater.HasInlinePatches(content);
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
                var result = KustomizeUpdater.HasInlineStrategicMergePatches(content);
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
                var result = KustomizeUpdater.HasInlineStrategicMergePatches(content);
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
                var result = KustomizeUpdater.HasInlineStrategicMergePatches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineStrategicMergePatches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var result = KustomizeUpdater.HasInlineStrategicMergePatches(content);
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
                var result = KustomizeUpdater.HasInlineJson6902Patches(content);
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
                var result = KustomizeUpdater.HasInlineJson6902Patches(content);
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
                var result = KustomizeUpdater.HasInlineJson6902Patches(content);
                result.Should().BeFalse();
            }

            [Test]
            public void HasInlineJson6902Patches_InvalidYaml_ReturnsFalse()
            {
                const string content = @"invalid: yaml: [unclosed";
                var result = KustomizeUpdater.HasInlineJson6902Patches(content);
                result.Should().BeFalse();
            }
        }
    }
}