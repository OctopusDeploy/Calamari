using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsTagBuilderTests
{
    [Test]
    public void Build_EmitsDefaultTag_WhenOctopusVariableHasValue()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Project.Name", "MyProject");

        var tags = EcsTagBuilder.Build(variables, []);

        tags.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Tag { Key = "Octopus.Project.Name", Value = "MyProject" });
    }

    [Test]
    public void Build_OmitsDefaultTag_WhenOctopusVariableIsUnset()
    {
        var tags = EcsTagBuilder.Build(new CalamariVariables(), []);

        tags.Should().BeEmpty();
    }

    [Test]
    public void Build_TruncatesKeyLongerThan128Chars()
    {
        var userTags = new[] { new Tag { Key = new string('k', 200), Value = "v" } };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Key.Length.Should().Be(128);
    }

    [Test]
    public void Build_TruncatesValueLongerThan256Chars()
    {
        var userTags = new[] { new Tag { Key = "k", Value = new string('v', 300) } };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Value.Length.Should().Be(256);
    }

    [Test]
    public void Build_StripsInvalidCharactersFromKeyAndValue()
    {
        var userTags = new[] { new Tag { Key = "my!@#$key", Value = "val!ue%" } };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Key.Should().Be("my@key");
        tags[0].Value.Should().Be("value");
    }
}
