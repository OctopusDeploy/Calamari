using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD;

[TestFixture]
public class InlineStrategicMergeTest
{
    readonly ILog log = new InMemoryLog();
    [Test]
    public void UpdateImages_ChangesOnlyTheImageReference_PreservingCommentsAndLineEndings()
    {
        const string content = "# strategic merge patches\r\n"
                               + "apiVersion: kustomize.config.k8s.io/v1beta1\r\n"
                               + "kind: Kustomization\r\n"
                               + "\r\n"
                               + "patchesStrategicMerge:\r\n"
                               + "  - |\r\n"
                               + "    apiVersion: apps/v1\r\n"
                               + "    kind: Deployment\r\n"
                               + "    spec:\r\n"
                               + "      template:\r\n"
                               + "        spec:\r\n"
                               + "          containers:\r\n"
                               + "            - name: nginx   # the web tier\r\n"
                               + "              image: nginx:1.21\r\n";

        var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
        };

        var replacer = new InlineStrategicMergeImageReplacer(content, "default-registry", log);

        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be("nginx:1.25");
        result.UpdatedContents.Should().Be(content.Replace("nginx:1.21", "nginx:1.25"));
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

        var replacer = new InlineStrategicMergeImageReplacer(content, "default-registry", log);
        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().Contain("nginx:1.25");
        result.UpdatedContents.Should().Contain("nginx:1.25");
    }

    [Theory]
    [TestCase("docker.io/nginx:1.27.1", "docker.io/nginx:1.28.0")]
    [TestCase("nginx:1.27.1", "nginx:1.28.0")]
    [TestCase("us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v1",
              "us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v2")]
    public void ReturnsSameImageBaseAsInYaml(string originalImage, string expectedImage)
    {
        var content = $@"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesStrategicMerge:
- |
  apiVersion: apps/v1
  kind: Deployment
  spec:
    template:
      spec:
        containers:
        - name: my-container
          image: {originalImage}";

        var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString(expectedImage, ArgoCDConstants.DefaultContainerRegistry))
        };

        var replacer = new InlineStrategicMergeImageReplacer(content, ArgoCDConstants.DefaultContainerRegistry, log);
        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be(expectedImage);
        result.UpdatedContents.Should().Contain(expectedImage);
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
        
        var replacer = new InlineStrategicMergeImageReplacer(content, "default-registry", log);
        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().BeEmpty();
        result.UpdatedContents.Should().Be(content);
    }
    
}