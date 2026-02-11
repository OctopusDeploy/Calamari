using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    public class KustomizeImageReplacerTests
    {
        readonly List<ContainerImageReference> imagesToUpdate = new List<ContainerImageReference>()
        {
            // We know this won't be null after parse
            ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry),
            ContainerImageReference.FromReferenceString("busybox:stable", "my-registry.com"),
        };

        ILog log = new InMemoryLog();

        [Test]
        public void UpdateImages_WithQualifiedNameOnly_AddsNewTagNode()
        {
            const string inputYaml = @"
images:
 - name: ""docker.io/nginx""
";

            const string expectedYaml = @"
images:
- name: ""docker.io/nginx""
  newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithUnqualifiedName_AddsNewTagNode()
        {
            const string inputYaml = @"
images:
- name: nginx
";

            const string expectedYaml = @"
images:
- name: nginx
  newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithUnqualifiedNameAndIndentedSequence_AddsNewTagNode()
        {
            const string inputYaml = @"
images:
  - name: nginx
";

            const string expectedYaml = @"
images:
  - name: nginx
    newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_NewTagNodeExists_UpdatesNewTagNode()
        {
            const string inputYaml = @"
images:
- name: nginx
  newTag: ""1.66""
";

            const string expectedYaml = @"
images:
- name: nginx
  newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_NewTagNodeExistsWithIndentedSequence_UpdatesNewTagNode()
        {
            const string inputYaml = @"
images:
  - name: nginx
    newTag: ""1.66""
";

            const string expectedYaml = @"
images:
  - name: nginx
    newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_NewTagNodeExists_MaintainsQuotes()
        {
            const string inputYaml = @"
images:
- name: nginx
  newTag: '1.66'
";

            const string expectedYaml = @"
images:
- name: nginx
  newTag: '1.25'
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_NewTagNodeExists_AddsQuotes()
        {
            const string inputYaml = @"
images:
- name: nginx
  newTag: v1.66
";

            const string expectedYaml = @"
images:
- name: nginx
  newTag: ""1.25""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WhenRegistryDoesNotMatch_DoesNotUpdateNewTagNode()
        {
            const string inputYaml = @"
images:
- name: not.docker.io/nginx
  newTag: ""1.66""
";

            const string expectedYaml = @"
images:
- name: not.docker.io/nginx
  newTag: ""1.66""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(0);
        }

        [Test]
        public void UpdateImages_ExistingContainerHasDigest_StripsDigestAndAddsNewTagNode()
        {
            const string inputYaml = @"
images:
- name: my-registry.com/busybox
  digest: sha256:24a0c4b4a4c0eb97a1aabb8e29f18e917d05abfe1b7a7c07857230879ce7d3d3
";

            const string expectedYaml = @"
images:
- name: my-registry.com/busybox
  newTag: ""stable""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
        }

        [Test]
        public void UpdateImages_HasNewNameNode_PreferencesNewNameNodeWhenMatching()
        {
            const string inputYaml = @"
images:
- name: busybox
  newName: my-registry.com/busybox
";

            const string expectedYaml = @"
images:
- name: busybox
  newName: my-registry.com/busybox
  newTag: ""stable""
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
        }

        [Test]
        public void UpdateImages_MultipleImages_AllCorrectlyUpdated()
        {
            const string inputYaml = @"
images:
- name: busybox
  newName: my-registry.com/busybox
- name: nginx
  newTag: ""1.66""
- name: not.docker.io/nginx
  newTag: vCool
";

            const string expectedYaml = @"
images:
- name: busybox
  newName: my-registry.com/busybox
  newTag: ""stable""
- name: nginx
  newTag: ""1.25""
- name: not.docker.io/nginx
  newTag: vCool
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_EmptyYamlContent_LogsAppropriateWarning()
        {
            var inMemoryLog = new InMemoryLog();
            var imageReplacer = new KustomizeImageReplacer("", ArgoCDConstants.DefaultContainerRegistry, inMemoryLog);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be("");
            result.UpdatedImageReferences.Count.Should().Be(0);
            inMemoryLog.StandardOut.Should().Contain("Kustomization file content is empty or whitespace only.");
        }

        [Test]
        public void UpdateImages_WhitespaceOnlyYamlContent_LogsAppropriateWarning()
        {
            const string whitespaceYaml = "   \n  \t  \n  ";
            var inMemoryLog = new InMemoryLog();
            var imageReplacer = new KustomizeImageReplacer(whitespaceYaml, ArgoCDConstants.DefaultContainerRegistry, inMemoryLog);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(whitespaceYaml);
            result.UpdatedImageReferences.Count.Should().Be(0);
            inMemoryLog.StandardOut.Should().Contain("Kustomization file content is empty or whitespace only.");
        }

        [Test]
        public void UpdateImages_MultipleYamlDocuments_LogsAppropriateWarning()
        {
            const string multiDocumentYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: test-config
";
            var inMemoryLog = new InMemoryLog();
            var imageReplacer = new KustomizeImageReplacer(multiDocumentYaml, ArgoCDConstants.DefaultContainerRegistry, inMemoryLog);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(multiDocumentYaml);
            result.UpdatedImageReferences.Count.Should().Be(0);
            inMemoryLog.StandardOut.Should().Contain("Kustomization file must contain exactly one YAML document with a mapping root node.");
        }

        [Test]
        public void UpdateImages_YamlSequenceAtRoot_LogsAppropriateWarning()
        {
            const string sequenceRootYaml = @"
- name: nginx
  newTag: '1.25'
- name: busybox
  newTag: 'stable'
";
            var inMemoryLog = new InMemoryLog();
            var imageReplacer = new KustomizeImageReplacer(sequenceRootYaml, ArgoCDConstants.DefaultContainerRegistry, inMemoryLog);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(sequenceRootYaml);
            result.UpdatedImageReferences.Count.Should().Be(0);
            inMemoryLog.StandardOut.Should().Contain("Kustomization file must contain exactly one YAML document with a mapping root node.");
        }

        [Test]
        public void UpdateImages_FullKustomizationFile_ShouldOnlyChangeTheImagesNode()
        {
            const string inputYaml = @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
metadata:
  name: arbitrary

" + @"# Example configuration for the webserver
" + @"# at https://github.com/monopole/hello
commonLabels:
  app: hello

images:
  - name: monopole
    newTag: '1'
  - name: nginx
    newName: docker.io/nginx

resources:
- deployment.yaml
- service.yaml

- configMap.yaml
";

            string expectedYaml = $@"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
metadata:
  name: arbitrary

" + @"# Example configuration for the webserver
" + @"# at https://github.com/monopole/hello
commonLabels:
  app: hello

images:
  - name: monopole
    newTag: '100'
  - name: nginx
    newName: docker.io/nginx
    newTag: ""1.25""

resources:
- deployment.yaml
- service.yaml

- configMap.yaml
";

            var imageReplacer = new KustomizeImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate.Append(ContainerImageReference.FromReferenceString("monopole:100")).ToList());

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "monopole:100");
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }
    }
}
