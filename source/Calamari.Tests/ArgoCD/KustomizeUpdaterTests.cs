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
            public void DeterminePatchTypeFromFile_UnknownFormat_ReturnsStrategicMergeDefault()
            {
                const string content = @"some: unknown
format: that
doesnt: match
patterns: true";
                var result = KustomizeUpdater.DeterminePatchTypeFromFile(content, "unknown.yaml");
                result.Should().Be(PatchType.StrategicMerge);
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
    }
}