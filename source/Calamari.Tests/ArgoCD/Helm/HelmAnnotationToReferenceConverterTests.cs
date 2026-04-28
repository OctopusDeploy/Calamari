using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm;

public class HelmAnnotationToReferenceConverterTests
{
    const string DefaultRegistry = "docker.io";
    readonly InMemoryLog log = new();

    HelmAnnotationToReferenceConverter CreateConverter() => new(DefaultRegistry, log);

    [Test]
    public void SeparatedTemplate_ResolvesToTagPath()
    {
        var converter = CreateConverter();
        const string yaml = @"
image:
  repository: nginx
  tag: 1.0
";
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = converter.Resolve(yaml, ["{{ .Values.image.repository }}:{{ .Values.image.tag }}"], images);

        result.Should().HaveCount(1);
        result.Should().ContainSingle(r => r.HelmReference == "image.tag");
        result.Should().ContainSingle(r => r.ContainerReference.Tag == "1.27.1");
    }

    [Test]
    public void CombinedTemplate_ResolvesToNamePath()
    {
        var converter = CreateConverter();
        const string yaml = @"
image:
  name: nginx:1.0
";
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = converter.Resolve(yaml, ["{{ .Values.image.name }}"], images);

        result.Should().HaveCount(1);
        result.Should().ContainSingle(r => r.HelmReference == "image.name");
    }

    [Test]
    public void NoMatchingImage_ReturnsEmpty()
    {
        var converter = CreateConverter();
        const string yaml = @"
image:
  repository: alpine
  tag: 3.18
";
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = converter.Resolve(yaml, ["{{ .Values.image.repository }}:{{ .Values.image.tag }}"], images);

        result.Should().BeEmpty();
    }

    [Test]
    public void MultipleTemplates_OnlySomeMatch_ReturnsOnlyMatched()
    {
        var converter = CreateConverter();
        const string yaml = @"
image1:
  name: nginx:1.0
image2:
  name: alpine:3.18
";
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = converter.Resolve(yaml, ["{{ .Values.image1.name }}", "{{ .Values.image2.name }}"], images);

        result.Should().HaveCount(1);
        result.Should().ContainSingle(r => r.HelmReference == "image1.name");
    }

    [Test]
    public void EmptyYaml_ReturnsEmpty()
    {
        var converter = CreateConverter();

        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = converter.Resolve("", ["{{ .Values.image.tag }}"], images);

        result.Should().BeEmpty();
    }
}
