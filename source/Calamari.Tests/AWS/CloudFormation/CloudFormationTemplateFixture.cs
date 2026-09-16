using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using FluentAssertions;
using Newtonsoft.Json;
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
        var parametersFile = Some.String();
        var templateFile = Some.String();
        
        var fileContent = new[]
        {
            new { ParameterKey = "Foo", ParameterValue = "FromFile" },
            new { ParameterKey = "Bar", ParameterValue = "FromFile" }
        };
        fileSystem.ReadFile(parametersFile).Returns(JsonConvert.SerializeObject(fileContent));
        fileSystem.ReadFile(templateFile).Returns(Some.String());

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve(templateFile, false, Arg.Any<IVariables>())
                        .Returns(new ResolvedTemplatePath(templateFile));
        templateResolver.MaybeResolve(parametersFile, false, Arg.Any<IVariables>())
                        .Returns(new ResolvedTemplatePath(parametersFile).AsSome());

        var variables = new CalamariVariables();
        var overrides = new List<Parameter>
        {
            new() { ParameterKey = "Foo", ParameterValue = "Overridden" },
            new() { ParameterKey = "Baz", ParameterValue = "New" }
        };

        var builder = CloudFormationTemplate.Create(templateResolver,
            templateFile,
            parametersFile,
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

        builder.Inputs.Should()
               .BeEquivalentTo(new[]
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
        var parametersFile = Some.String();
        var templateFile = Some.String();

        var fileContent = new[]
        {
            new { ParameterKey = "Foo", ParameterValue = "FromFile" },
        };
        fileSystem.ReadFile(templateFile).Returns(Some.String());
        fileSystem.ReadFile(parametersFile).Returns(JsonConvert.SerializeObject(fileContent));

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve(templateFile, false, Arg.Any<IVariables>())
                        .Returns(new ResolvedTemplatePath(templateFile));
        templateResolver.MaybeResolve(parametersFile, false, Arg.Any<IVariables>())
                        .Returns(new ResolvedTemplatePath(parametersFile).AsSome());

        var variables = new CalamariVariables();

        var builder = CloudFormationTemplate.Create(templateResolver,
            templateFile,
            parametersFile,
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

        builder.Inputs.Should().BeEquivalentTo([new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" }]);
    }
}
