using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation.Templates;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationParameterMergeFixture
{
    [Test]
    public void Merge_OverrideReplacesMatchingKey()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var result = CloudFormationParameterMerge.Merge(primary, overrides);

        result.Should().BeEquivalentTo(new[] { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } });
    }

    [Test]
    public void Merge_OverrideAppendsNewKey()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Bar", ParameterValue = "New" } };

        var result = CloudFormationParameterMerge.Merge(primary, overrides);

        result.Should().BeEquivalentTo(new[]
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" },
            new Parameter { ParameterKey = "Bar", ParameterValue = "New" }
        });
    }

    [Test]
    public void Merge_EmptyOverrides_ReturnsPrimaryUnchanged()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };

        var result = CloudFormationParameterMerge.Merge(primary, new List<Parameter>());

        result.Should().BeEquivalentTo(primary);
    }

    [Test]
    public void Merge_EmptyPrimary_ReturnsOverridesOnly()
    {
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var result = CloudFormationParameterMerge.Merge(new List<Parameter>(), overrides);

        result.Should().BeEquivalentTo(overrides);
    }
}
