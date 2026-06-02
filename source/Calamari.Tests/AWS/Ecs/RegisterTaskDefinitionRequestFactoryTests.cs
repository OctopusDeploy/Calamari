using System.Collections.Generic;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
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

    static IVariables Variables(params (string packageReference, string image)[] packages)
    {
        var variables = new CalamariVariables();
        foreach (var (packageReference, image) in packages)
        {
            variables.Set(PackageVariables.IndexedImage(packageReference), image);
        }
        return variables;
    }

    static RegisterTaskDefinitionRequest Build(TaskDefinition template, ContainerUpdate[] updates, IVariables variables = null) =>
        RegisterTaskDefinitionRequestFactory.FromTaskDefinition(
            template,
            targetFamily: "fam",
            updates,
            tags: [],
            variables ?? new CalamariVariables());

    [Test]
    public void DoesNotMutateInputTemplate()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new ContainerUpdate { ContainerName = "web", PackageReference = "web-pkg" }
        };

        var request = Build(template, updates, Variables(("web-pkg", "new:2")));

        template.ContainerDefinitions[0].Image.Should().Be("old:1");
        request.ContainerDefinitions[0].Image.Should().Be("new:2");
    }

    [Test]
    public void AppliesTargetFamilyAndTags()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new ContainerUpdate { ContainerName = "web", PackageReference = "web-pkg" }
        };

        var request = RegisterTaskDefinitionRequestFactory.FromTaskDefinition(
            template,
            targetFamily: "different-fam",
            updates,
            tags: [new KeyValuePair<string, string>("Owner", "platform")],
            Variables(("web-pkg", "new:2")));

        request.Family.Should().Be("different-fam");
        request.Tags.Should().ContainSingle().Which.Key.Should().Be("Owner");
    }

    [Test]
    public void NoMatchingContainerThrows()
    {
        var template = Template(new ContainerDefinition { Name = "web" });
        var updates = new[]
        {
            new ContainerUpdate { ContainerName = "api", PackageReference = "api-pkg" }
        };

        var act = () => Build(template, updates, Variables(("api-pkg", "x:1")));
        act.Should().Throw<CommandException>().WithMessage("*No matching container*");
    }

    [Test]
    public void PreservesExistingImageWhenPackageReferenceEmpty()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new ContainerUpdate { ContainerName = "web", PackageReference = null }
        };

        var request = Build(template, updates, Variables(("web-pkg", "should-not-be-used")));

        request.ContainerDefinitions[0].Image.Should().Be("old:1");
    }

    [Test]
    public void PreservesExistingImageWhenImageVariableMissing()
    {
        var template = Template(new ContainerDefinition { Name = "web", Image = "old:1" });
        var updates = new[]
        {
            new ContainerUpdate { ContainerName = "web", PackageReference = "web-pkg" }
        };

        var request = Build(template, updates);

        request.ContainerDefinitions[0].Image.Should().Be("old:1");
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
            new ContainerUpdate
            {
                ContainerName = "web",
                EnvironmentVariables = new EnvAction<TypedKeyValuePair>
                {
                    Action = EnvActionMode.Replace,
                    Items = [new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "NEW", Value = "1" }]
                }
            }
        };

        var request = Build(template, updates);
        var env = request.ContainerDefinitions[0].Environment;

        env.Should().ContainSingle().Which.Name.Should().Be("NEW");
    }

    [Test]
    public void EnvVarsAppendPrefersNewValueOnKeyCollision()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            Environment = [new Amazon.ECS.Model.KeyValuePair { Name = "K", Value = "old" }]
        });
        var updates = new[]
        {
            new ContainerUpdate
            {
                ContainerName = "web",
                EnvironmentVariables = new EnvAction<TypedKeyValuePair>
                {
                    Action = EnvActionMode.Append,
                    Items = [new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "K", Value = "new" }]
                }
            }
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
            new ContainerUpdate
            {
                ContainerName = "web",
                EnvironmentVariables = new EnvAction<TypedKeyValuePair>
                {
                    Action = EnvActionMode.Replace,
                    Items = [new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "S", Value = "arn:aws:ssm:::parameter/x" }]
                }
            }
        };

        var request = Build(template, updates);
        var secret = request.ContainerDefinitions[0].Secrets.Should().ContainSingle().Subject;

        secret.Name.Should().Be("S");
        secret.ValueFrom.Should().Be("arn:aws:ssm:::parameter/x");
    }

    [Test]
    public void EnvFilesAppendDedupesByValue()
    {
        var template = Template(new ContainerDefinition
        {
            Name = "web",
            EnvironmentFiles = [new EnvironmentFile { Type = "s3", Value = "arn:aws:s3:::bucket/file" }]
        });
        var updates = new[]
        {
            new ContainerUpdate
            {
                ContainerName = "web",
                EnvironmentFiles = new EnvAction<string>
                {
                    Action = EnvActionMode.Append,
                    Items = ["arn:aws:s3:::bucket/file"]
                }
            }
        };

        var request = Build(template, updates);

        request.ContainerDefinitions[0].EnvironmentFiles.Should().ContainSingle();
    }
}
