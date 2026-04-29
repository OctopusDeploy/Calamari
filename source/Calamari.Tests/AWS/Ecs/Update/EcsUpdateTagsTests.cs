using System.Collections.Generic;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class EcsUpdateTagsTests
{
    [Test]
    public void Merge_UserOverridesDefault_OnKeyCollision()
    {
        var v = new CalamariVariables();
        v.Set("Octopus.Project.Name", "MyProject");

        var userTags = new[] { new KeyValuePair<string, string>("Octopus.Project.Name", "Override") };

        var tags = EcsUpdateTags.Merge(v, userTags);

        tags.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new KeyValuePair<string, string>("Octopus.Project.Name", "Override"));
    }

    [Test]
    public void Merge_UnsetDefaults_AreOmitted()
    {
        var tags = EcsUpdateTags.Merge(new CalamariVariables(), []);

        tags.Should().BeEmpty();
    }

    [Test]
    public void Merge_TruncatesKeyAndValueToAwsLimits()
    {
        var userTags = new[] { new KeyValuePair<string, string>(new string('k', 200), new string('v', 300)) };

        var tags = EcsUpdateTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Length.Should().Be(128);
        tags[0].Value.Length.Should().Be(256);
    }

    [Test]
    public void Merge_StripsInvalidCharacters()
    {
        var userTags = new[] { new KeyValuePair<string, string>("my!@#$key", "val!ue%") };

        var tags = EcsUpdateTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Should().Be("my@key");
        tags[0].Value.Should().Be("value");
    }

    [Test]
    public void Merge_DropsTagsThatSanitiseToEmpty()
    {
        var userTags = new[]
        {
            new KeyValuePair<string, string>("!!!", "value"),
            new KeyValuePair<string, string>("validKey", "validValue")
        };

        var tags = EcsUpdateTags.Merge(new CalamariVariables(), userTags);

        tags.Should().ContainSingle().Which.Key.Should().Be("validKey");
    }
}
