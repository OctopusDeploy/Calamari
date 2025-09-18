#if NET
using System;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.ArgoCD.Commands.Conventions.UpdateArgoCdAppImages.Models;

public class TemplatedImagePathTests
{

    [Test]
    public void Parse_WithTagSeparatorDefinedInTemplate_PopulatesTagWithValueAndSetsTagIsTemplateTokenToTrue()
    {
        const string template = "{{ .Values.some-other-value }}:{{ .Values.tag-token }}";

        var sut = TemplatedImagePath.Parse(template, new VariableDictionary { { "fully-qualified-image-reference", "docker.io/nginx:1.27" } }, "docker.io");

        sut.TagIsTemplateToken.Should().BeTrue();
        sut.TagPath.Should().Be("tag-token");

    }

    [Test]
    public void Parse_WithNotTagSeparatorInTemplate_PopulatesTagValueWithLastTokenAndSetsTagIsTokenToFalse()
    {
        const string template = "{{ .Values.registry }}/{{ .Values.image-with-tag-specified }}";

        var sut = TemplatedImagePath.Parse(template, new VariableDictionary { { "fully-qualified-image-reference", "docker.io/nginx:1.27" } }, "docker.io");

        sut.TagIsTemplateToken.Should().BeFalse();
        sut.TagPath.Should().Be("image-with-tag-specified");
    }

    [Test]
    public void Parse_WithSingleTokenInTemplate_PopulatesTagWithTemplateAndSetsTagIsTemplateTokenToFalse()
    {
        const string template = "{{ .Values.fully-qualified-image-reference }}";

        var sut = TemplatedImagePath.Parse(template, new VariableDictionary { { "fully-qualified-image-reference", "docker.io/nginx:1.27" } }, "docker.io");

        sut.TagIsTemplateToken.Should().BeFalse();
        sut.TagPath.Should().Be("fully-qualified-image-reference");
    }

    [Test]
    public void Parse_WithSingleTokenTemplate_AndMatchingVariables_PopulatesImageReference()
    {
        const string template = "{{ .Values.fully-qualified-image-reference }}";
        var variables = new VariableDictionary { { "fully-qualified-image-reference", "docker.io/nginx:1.27" } };

        var sut = TemplatedImagePath.Parse(template, variables, "docker.io");
        var imageReference = sut.ImageReference;

        imageReference.Registry.Should().Be("docker.io");
        imageReference.ImageName.Should().Be("nginx");
        imageReference.Tag.Should().Be("1.27");
    }

    [Test]
    public void Parse_WithQualifiedImageAndTagInTemplate_AndMatchingVariables_PopulatesImageReference()
    {
        const string template = "{{ .Values.image-with-registry }}:{{ .Values.image-tag }}";
        var variables = new VariableDictionary
        {
            { "image-with-registry", "docker.io/nginx" },
            { "image-tag", "1.27" }
        };

        var sut = TemplatedImagePath.Parse(template, variables, "docker.io");
        var imageReference = sut.ImageReference;

        imageReference.Registry.Should().Be("docker.io");
        imageReference.ImageName.Should().Be("nginx");
        imageReference.Tag.Should().Be("1.27");
    }

    [Test]
    public void Parse_WithMultipleTokenInTemplate_AndMatchingVariables_PopulatesImageReference()
    {
        const string template = "{{ .Values.registry }}/{{ .Values.path }}/{{ .Values.image }}:{{ .Values.image-tag }}";
        var variables = new VariableDictionary
        {
            { "registry", "customreg.io" },
            { "path", "webstuff" },
            { "image", "nginx" },
            { "image-tag", "1.22" }
        };

        var sut = TemplatedImagePath.Parse(template, variables, "docker.io");
        var imageReference = sut.ImageReference;

        imageReference.Registry.Should().Be("customreg.io");
        imageReference.ImageName.Should().Be("webstuff/nginx");
        imageReference.Tag.Should().Be("1.22");
    }

    [Test]
    public void Parse_WithEmptyTemplate_ThrowsArgumentException()
    {
        var parse = () => TemplatedImagePath.Parse("", new VariableDictionary(), "");

        parse.Should().Throw<ArgumentException>()
            .Where(e => e.ParamName == "template");
    }
    
    [Test]
    public void Parse_WithEmptyVariableDictionary_ThrowsArgumentException()
    {
        var parse = () => TemplatedImagePath.Parse("{{ .Values.registry }}", new VariableDictionary(), "");

        parse.Should().Throw<ArgumentException>()
            .Where(e => e.ParamName == "variables");
    }
    
    [Test]
    public void Parse_WithEmptyDefaultRegistry_ThrowsArgumentException()
    {
        var parse = () => TemplatedImagePath.Parse("{{ .Values.registry }}",  new VariableDictionary { { "fully-qualified-image-reference", "docker.io/nginx:1.27" } }, "");

        parse.Should().Throw<ArgumentException>()
            .Where(e => e.ParamName == "defaultRegistry");
    }

    [Test]
    public void Parse_WithInvalidTemplateProperties_ThrowsInvalidOperationException()
    {
        const string template = "{{ .Values.fully-qualified-image-reference }}";
        var variables = new VariableDictionary { { "fully-qualified-image-reference", "this is not a valid container reference" } };

        var parse = () => TemplatedImagePath.Parse(template, variables, "docker.io");

        parse.Should().Throw<InvalidOperationException>();
    }
}
#endif