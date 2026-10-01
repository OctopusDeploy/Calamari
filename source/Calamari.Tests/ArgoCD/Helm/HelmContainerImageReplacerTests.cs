using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm;

[TestFixture]
public class HelmContainerImageReplacerTests
{
    const string DefaultRegistry = "docker.io";
    readonly InMemoryLog log = new();

    [Theory]
    [TestCase("docker.io/nginx:1.27.1", "docker.io/nginx:1.28.0")]
    [TestCase("nginx:1.27.1", "nginx:1.28.0")]
    [TestCase("us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v1",
              "us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v2")]
    public void ReturnsSameImageBaseAsInYaml(string originalImage, string expectedImage)
    {
        var yaml = $@"image: {originalImage}
";
        var annotations = new[] { "{{ .Values.image }}" };
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, annotations, log);

        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString(expectedImage, DefaultRegistry))
        };

        var result = replacer.UpdateImages(images);

        result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be(expectedImage);
        result.UpdatedContents.Should().Contain($"image: {expectedImage}");
    }
}
