using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationS3TemplateFixture
{
    [Test]
    public void Create_WithNoParametersS3Url_UsesOverridesOnly()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        var variables = new CalamariVariables();
        var log = Substitute.For<ILog>();
        var overrides = new List<Parameter> { new()
            { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var builder = CloudFormationS3Template.Create("https://example.s3.amazonaws.com/template.yaml",
                                                       null,
                                                       overrides,
                                                       fileSystem,
                                                       variables,
                                                       log,
                                                       "my-stack",
                                                       new List<string>(),
                                                       false,
                                                       null,
                                                       new List<KeyValuePair<string, string>>(),
                                                       new StackArn("my-stack"),
                                                       () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(overrides);
    }
}
