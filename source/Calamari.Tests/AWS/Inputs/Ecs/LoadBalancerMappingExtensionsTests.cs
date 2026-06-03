using System;
using Calamari.Aws.Inputs.Ecs;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Tests.AWS.Inputs.Ecs;

[TestFixture]
public class LoadBalancerMappingExtensionsTests
{
    [Test]
    public void ToLoadBalancerProperties_WhenEmpty_ReturnsNull()
    {
        var result = Array.Empty<LoadBalancerMapping>().ToLoadBalancerProperties();

        result.Should().BeNull();
    }

    [Test]
    public void ToLoadBalancerProperties_MapsAllFields()
    {
        var mappings = new[]
        {
            new LoadBalancerMapping
            {
                ContainerName = "web",
                ContainerPort = "80",
                TargetGroupArn = "arn:aws:elasticloadbalancing:us-east-1:123:targetgroup/web/abc"
            }
        };

        var result = mappings.ToLoadBalancerProperties();

        result.Should().HaveCount(1);
        result[0].ContainerName.Should().Be("web");
        result[0].ContainerPort.Should().Be(80);
        result[0].TargetGroupArn.Should().Be("arn:aws:elasticloadbalancing:us-east-1:123:targetgroup/web/abc");
    }

    [Test]
    public void ToLoadBalancerProperties_WithEmptyContainerPort_ReturnsNullPort()
    {
        var mappings = new[]
        {
            new LoadBalancerMapping
            {
                ContainerName = "web",
                ContainerPort = string.Empty,
                TargetGroupArn = "arn:tg"
            }
        };

        var result = mappings.ToLoadBalancerProperties();

        result[0].ContainerPort.Should().BeNull();
    }

    [Test]
    public void ToLoadBalancerProperties_PreservesOrderAcrossMultipleMappings()
    {
        var mappings = new[]
        {
            new LoadBalancerMapping { ContainerName = "web", ContainerPort = "80", TargetGroupArn = "arn:web" },
            new LoadBalancerMapping { ContainerName = "api", ContainerPort = "8080", TargetGroupArn = "arn:api" },
            new LoadBalancerMapping { ContainerName = "admin", ContainerPort = "9090", TargetGroupArn = "arn:admin" }
        };

        var result = mappings.ToLoadBalancerProperties();

        result.Should().HaveCount(3);
        result[0].ContainerName.Should().Be("web");
        result[0].ContainerPort.Should().Be(80);
        result[1].ContainerName.Should().Be("api");
        result[1].ContainerPort.Should().Be(8080);
        result[2].ContainerName.Should().Be("admin");
        result[2].ContainerPort.Should().Be(9090);
    }

    [Test]
    public void ToLoadBalancerProperties_PassesThroughContainerNameAndArnVerbatim()
    {
        // Empty/whitespace strings are mapped through unchanged — no normalisation.
        var mappings = new[]
        {
            new LoadBalancerMapping
            {
                ContainerName = string.Empty,
                ContainerPort = "80",
                TargetGroupArn = string.Empty
            }
        };

        var result = mappings.ToLoadBalancerProperties();

        result[0].ContainerName.Should().BeEmpty();
        result[0].TargetGroupArn.Should().BeEmpty();
    }
}
