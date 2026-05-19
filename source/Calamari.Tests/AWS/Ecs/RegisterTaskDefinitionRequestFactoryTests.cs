using System.Collections.Generic;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class RegisterTaskDefinitionRequestFactoryTests
{
    static TaskDefinition Template(params ContainerDefinition[] containers) => new()
    {
        Family = "fam",
        ContainerDefinitions = [..containers]
    };

    static RegisterTaskDefinitionRequest Build(TaskDefinition template, EcsContainerUpdate[] updates) =>
        RegisterTaskDefinitionRequestFactory.FromTaskDefinition(template, targetFamily: "fam", updates, tags: []);

    [Test]
    public void DoesNotMutateInputTemplate()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", "new:2", null, null)
        };

        var request = Build(template, updates);

        template.ContainerDefinitions[0].Image.Should().Be("old:1");
        request.ContainerDefinitions[0].Image.Should().Be("new:2");
    }

    [Test]
    public void AppliesTargetFamilyAndTags()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", "new:2", null, null)
        };

        var request = RegisterTaskDefinitionRequestFactory.FromTaskDefinition(
            template,
            targetFamily: "different-fam",
            updates,
            tags: [new KeyValuePair<string, string>("Owner", "platform")]);

        request.Family.Should().Be("different-fam");
        request.Tags.Should().ContainSingle().Which.Key.Should().Be("Owner");
    }

    [Test]
    public void NoMatchingContainerThrows()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new EcsContainerUpdate("api", "x:1", null, null)
        };

        var act = () => Build(template, updates);
        act.Should().Throw<CommandException>().WithMessage("*No matching container*");
    }

    [Test]
    public void EnvVarsReplaceOverwritesEntireSet()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            Environment = [new Amazon.ECS.Model.KeyValuePair { Name = "OLD", Value = "1" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvAction<EnvVarItem>(EnvActionMode.Replace, [new EnvVarItem(EnvVarType.Plain, "NEW", "1")]),
                null)
        };

        var request = Build(template, updates);
        var env = request.ContainerDefinitions[0].Environment;

        env.Should().ContainSingle().Which.Name.Should().Be("NEW");
    }

    [Test]
    public void EnvVarsMergePrefersNewValueOnKeyCollision()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            Environment = [new Amazon.ECS.Model.KeyValuePair { Name = "K", Value = "old" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvAction<EnvVarItem>(EnvActionMode.Merge, [new EnvVarItem(EnvVarType.Plain, "K", "new")]),
                null)
        };

        var request = Build(template, updates);
        request.ContainerDefinitions[0].Environment
               .Should().ContainSingle().Which.Value.Should().Be("new");
    }

    [Test]
    public void SecretsReplaceMapsValueFromCorrectly()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvAction<EnvVarItem>(EnvActionMode.Replace, [new EnvVarItem(EnvVarType.Secret, "S", "arn:aws:ssm:::parameter/x")]),
                null)
        };

        var request = Build(template, updates);
        var secret = request.ContainerDefinitions[0].Secrets.Should().ContainSingle().Subject;

        secret.Name.Should().Be("S");
        secret.ValueFrom.Should().Be("arn:aws:ssm:::parameter/x");
    }

    [Test]
    public void EnvFilesMergeDedupesByValue()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            EnvironmentFiles = [new EnvironmentFile { Type = "s3", Value = "arn:aws:s3:::bucket/file" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null, null,
                new EnvAction<string>(EnvActionMode.Merge, ["arn:aws:s3:::bucket/file"]))
        };

        var request = Build(template, updates);

        request.ContainerDefinitions[0].EnvironmentFiles.Should().ContainSingle();
    }
}
