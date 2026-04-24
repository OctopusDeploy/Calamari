using System.Collections.Generic;
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
            .Which.Should().BeEquivalentTo(new KeyValuePair<string, string>("Octopus.Project.Name", "MyProject"));
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
        var userTags = new[] { new KeyValuePair<string, string>(new string('k', 200), "v") };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Key.Length.Should().Be(128);
    }

    [Test]
    public void Build_TruncatesValueLongerThan256Chars()
    {
        var userTags = new[] { new KeyValuePair<string, string>("k", new string('v', 300)) };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Value.Length.Should().Be(256);
    }

    [Test]
    public void Build_StripsInvalidCharactersFromKeyAndValue()
    {
        var userTags = new[] { new KeyValuePair<string, string>("my!@#$key", "val!ue%") };

        var tags = EcsTagBuilder.Build(new CalamariVariables(), userTags);

        tags[0].Key.Should().Be("my@key");
        tags[0].Value.Should().Be("value");
    }
}
