using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.CoreUtilities;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationTemplateFixture
{
    [Test]
    public void Create_MergesParameterOverridesOverFileParameters()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        fileSystem.ReadFile("resolved/template.yaml").Returns("template content");
        fileSystem.ReadFile("resolved/parameters.json")
                   .Returns("[{\"ParameterKey\":\"Foo\",\"ParameterValue\":\"FromFile\"},{\"ParameterKey\":\"Bar\",\"ParameterValue\":\"FromFile\"}]");

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve("template.yaml", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/template.yaml"));
        templateResolver.MaybeResolve("parameters.json", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/parameters.json").AsSome());

        var variables = new CalamariVariables();
        var overrides = new List<Parameter>
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" },
            new Parameter { ParameterKey = "Baz", ParameterValue = "New" }
        };

        var builder = CloudFormationTemplate.Create(templateResolver,
                                                     "template.yaml",
                                                     "parameters.json",
                                                     overrides,
                                                     false,
                                                     fileSystem,
                                                     variables,
                                                     "my-stack",
                                                     new List<string>(),
                                                     false,
                                                     null,
                                                     new List<KeyValuePair<string, string>>(),
                                                     new StackArn("my-stack"),
                                                     () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(new[]
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" },
            new Parameter { ParameterKey = "Bar", ParameterValue = "FromFile" },
            new Parameter { ParameterKey = "Baz", ParameterValue = "New" }
        });
    }

    [Test]
    public void Create_WithNoOverrides_ReturnsFileParametersUnchanged()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        fileSystem.ReadFile("resolved/template.yaml").Returns("template content");
        fileSystem.ReadFile("resolved/parameters.json")
                   .Returns("[{\"ParameterKey\":\"Foo\",\"ParameterValue\":\"FromFile\"}]");

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve("template.yaml", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/template.yaml"));
        templateResolver.MaybeResolve("parameters.json", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/parameters.json").AsSome());

        var variables = new CalamariVariables();

        var builder = CloudFormationTemplate.Create(templateResolver,
                                                     "template.yaml",
                                                     "parameters.json",
                                                     new List<Parameter>(),
                                                     false,
                                                     fileSystem,
                                                     variables,
                                                     "my-stack",
                                                     new List<string>(),
                                                     false,
                                                     null,
                                                     new List<KeyValuePair<string, string>>(),
                                                     new StackArn("my-stack"),
                                                     () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(new[] { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } });
    }
}
