using System.Collections.Generic;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsDefaultTagsTests
{
    [Test]
    public void EmitsDefaultTag_WhenOctopusVariableHasValue()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Project.Name", "MyProject");

        var tags = EcsDefaultTags.Merge(variables, []);

        tags.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new KeyValuePair<string, string>("Octopus.Project.Name", "MyProject"));
    }

    [Test]
    public void OmitsDefaultTag_WhenOctopusVariableIsUnset()
    {
        var tags = EcsDefaultTags.Merge(new CalamariVariables(), []);

        tags.Should().BeEmpty();
    }

    [Test]
    public void TruncatesKeyLongerThan128Chars()
    {
        var userTags = new[] { new KeyValuePair<string, string>(new string('k', 200), "v") };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Length.Should().Be(128);
    }

    [Test]
    public void TruncatesValueLongerThan256Chars()
    {
        var userTags = new[] { new KeyValuePair<string, string>("k", new string('v', 300)) };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags[0].Value.Length.Should().Be(256);
    }

    [Test]
    public void StripsInvalidCharactersFromKeyAndValue()
    {
        var userTags = new[] { new KeyValuePair<string, string>("my!@#$key", "val!ue%") };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Should().Be("my@key");
        tags[0].Value.Should().Be("value");
    }

    [Test]
    public void MergesDefaultsThenUserTags_PreservingOrder()
    {
        var variables = new CalamariVariables();
        // Environment.Name is declared before Project.Name in OctopusVariableNames,
        // so it appears first in the output.
        variables.Set("Octopus.Environment.Name", "Production");
        variables.Set("Octopus.Project.Name", "MyProject");

        var userTags = new[]
        {
            new KeyValuePair<string, string>("Owner", "team@example.com")
        };

        var tags = EcsDefaultTags.Merge(variables, userTags);

        tags.Should().HaveCount(3);
        tags[0].Key.Should().Be("Octopus.Environment.Name");
        tags[1].Key.Should().Be("Octopus.Project.Name");
        tags[2].Key.Should().Be("Owner");
    }

    [Test]
    public void DropsTag_WhenSanitizeProducesEmptyKey()
    {
        // All-invalid-chars key sanitizes to "", which the final Where() filter drops
        // so AWS isn't sent a tag with an empty key.
        var userTags = new[]
        {
            new KeyValuePair<string, string>("!!!", "value"),
            new KeyValuePair<string, string>("validKey", "validValue")
        };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags.Should().ContainSingle().Which.Key.Should().Be("validKey");
    }
}
