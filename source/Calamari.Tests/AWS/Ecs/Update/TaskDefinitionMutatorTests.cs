using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs.Update;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class TaskDefinitionMutatorTests
{
    static TaskDefinition Template(params ContainerDefinition[] containers) => new()
    {
        Family = "fam",
        ContainerDefinitions = [..containers]
    };

    [Test]
    public void Apply_ReplacesImageOnNamedContainer()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });

        var updates = new[]
        {
            new EcsContainerUpdate("web", "new:2", EnvVarAction.None, EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);

        mutated.ContainerDefinitions.Should().ContainSingle()
               .Which.Image.Should().Be("new:2");
    }

    [Test]
    public void Apply_NoMatchingContainer_Throws()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new EcsContainerUpdate("api", "x:1", EnvVarAction.None, EnvFileAction.None)
        };

        var act = () => TaskDefinitionMutator.Apply(template, updates);
        act.Should().Throw<CommandException>().WithMessage("*No matching container*");
    }

    [Test]
    public void Apply_EnvVarsReplace_OverwritesEntireSet()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            Environment = [new Amazon.ECS.Model.KeyValuePair { Name = "OLD", Value = "1" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvVarAction(EnvVarMode.Replace, [new EnvVarItem("NEW", "1", EnvVarKind.Plain)]),
                EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);
        var env = mutated.ContainerDefinitions[0].Environment;

        env.Should().ContainSingle().Which.Name.Should().Be("NEW");
    }

    [Test]
    public void Apply_EnvVarsAppend_PrefersNewValueOnKeyCollision()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            Environment = [new Amazon.ECS.Model.KeyValuePair { Name = "K", Value = "old" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvVarAction(EnvVarMode.Append, [new EnvVarItem("K", "new", EnvVarKind.Plain)]),
                EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);
        mutated.ContainerDefinitions[0].Environment
               .Should().ContainSingle().Which.Value.Should().Be("new");
    }

    [Test]
    public void Apply_SecretsReplace_MapsValueFromCorrectly()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null,
                new EnvVarAction(EnvVarMode.Replace, [new EnvVarItem("S", "arn:aws:ssm:::parameter/x", EnvVarKind.Secret)]),
                EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);
        var secret = mutated.ContainerDefinitions[0].Secrets.Should().ContainSingle().Subject;

        secret.Name.Should().Be("S");
        secret.ValueFrom.Should().Be("arn:aws:ssm:::parameter/x");
    }

    [Test]
    public void Apply_EnvFilesAppend_DedupesByValue()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            EnvironmentFiles = [new EnvironmentFile { Type = "s3", Value = "arn:aws:s3:::bucket/file" }]
        });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null, EnvVarAction.None,
                new EnvFileAction(EnvFileMode.Append, [new EnvFileItem("arn:aws:s3:::bucket/file")]))
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);

        mutated.ContainerDefinitions[0].EnvironmentFiles.Should().ContainSingle();
    }

    [Test]
    public void Apply_NullImage_LeavesContainerImageUntouched()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "keep:1" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null, EnvVarAction.None, EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);

        mutated.ContainerDefinitions[0].Image.Should().Be("keep:1");
    }

    [Test]
    public void Apply_ReplaceWithNoItems_LeavesEnvironmentNull()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new EcsContainerUpdate("web", null, new EnvVarAction(EnvVarMode.Replace, []), EnvFileAction.None)
        };

        var mutated = TaskDefinitionMutator.Apply(template, updates);

        mutated.ContainerDefinitions[0].Environment.Should().BeNull();
    }
}
