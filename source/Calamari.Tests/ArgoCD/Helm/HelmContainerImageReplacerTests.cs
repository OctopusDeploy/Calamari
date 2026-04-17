using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Testing.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm;

public class HelmContainerImageReplacerTests
{
    const string DefaultRegistry = "docker.io";
    readonly InMemoryLog log = new();

    [Test]
    public void UnstructuredValue_UpdatesTag_TracksWithFriendlyName()
    {
        const string yaml = @"
image:
  tag: 1.0
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "image.tag")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEquivalentTo(["nginx:1.27.1"]);
        result.UpdatedContents.Should().Contain("tag: 1.27.1");
    }

    [Test]
    public void UnstructuredValue_AlreadyAtTarget_TracksWithFriendlyName()
    {
        const string yaml = @"
image:
  tag: 1.27.1
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "image.tag")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEmpty();
        result.AlreadyUpToDateImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
    }

    [Test]
    public void StructuredValue_UpdatesFullRef()
    {
        const string yaml = @"
image:
  name: nginx:1.0
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "image.name")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().HaveCount(1);
        result.UpdatedContents.Should().Contain("name: nginx:1.27.1");
    }

    [Test]
    public void StructuredValue_AlreadyAtTarget_TracksWithFriendlyName()
    {
        const string yaml = @"
image:
  name: nginx:1.27.1
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "image.name")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEmpty();
        result.AlreadyUpToDateImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
    }

    [Test]
    public void TwoImagesWithSameTag_OnlyUpdatesConfiguredPath()
    {
        const string yaml = @"
nginx:
  tag: 1.0
redis:
  tag: 1.0
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "nginx.tag")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEquivalentTo(["nginx:1.27.1"]);
        result.UpdatedContents.Should().Contain("nginx:\n  tag: 1.27.1");
        result.UpdatedContents.Should().Contain("redis:\n  tag: 1.0");
    }

    [Test]
    public void NoHelmReference_SkipsImage()
    {
        const string yaml = @"
image:
  tag: 1.0
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry))
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEmpty();
        result.AlreadyUpToDateImages.Should().BeEmpty();
    }

    [Test]
    public void PathNotFoundInYaml_SkipsImage()
    {
        const string yaml = @"
image:
  tag: 1.0
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "nonexistent.path")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEmpty();
        result.UpdatedContents.Should().Be(yaml);
    }

    [Test]
    public void StructuredValue_MismatchedImageName_DoesNotUpdate()
    {
        const string yaml = @"
image:
  name: alpine:3.18
";
        var replacer = new HelmContainerImageReplacer(yaml, DefaultRegistry, log);
        var images = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.27.1", DefaultRegistry), "image.name")
        };

        var result = replacer.UpdateImages(images);

        using var scope = new AssertionScope();
        result.UpdatedImageReferences.Should().BeEmpty();
        result.UpdatedContents.Should().Contain("name: alpine:3.18");
    }
}
