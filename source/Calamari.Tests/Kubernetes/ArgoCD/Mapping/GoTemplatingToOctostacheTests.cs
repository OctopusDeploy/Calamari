using System;
using NUnit.Framework;

namespace Calamari.Tests.Kubernetes.ArgoCD.Mapping;

public class GoTemplatingToOctostacheConverterTests
{
    [Test]
    public void ConvertToOctostache_WithEmptyString_ReturnsEmptyString()
    {
        var input = string.Empty;
        
        var result = GoTemplatingToOctostacheConverter.ConvertToOctostache(input);

        result.Should().BeNullOrEmpty();
    }
    
    [Test]
    public void ConvertToOctostache_WithHelmValueSyntax_ReturnsOctostacheSyntax()
    {
        const string input = "{{ .Values.image.repository }}/{{ .Values.image.name }}:{{ .Values.image.tag }}";
        
        var result = GoTemplatingToOctostacheConverter.ConvertToOctostache(input);

        result.Should().Be("#{image.repository}/#{image.name}:#{image.tag}");
    }
}

