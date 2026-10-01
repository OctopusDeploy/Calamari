using System.Collections.Generic;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsDefaultTagsTests
{
    [Test]
    public void Sanitize_TruncatesKeyTo128AndValueTo256()
    {
        var userTags = new[] { new KeyValuePair<string, string>(new string('k', 200), new string('v', 300)) };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Length.Should().Be(128);
        tags[0].Value.Length.Should().Be(256);
    }

    [Test]
    public void Sanitize_StripsInvalidCharactersFromKeyAndValue()
    {
        var userTags = new[] { new KeyValuePair<string, string>("my!@#$key", "val!ue%") };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags[0].Key.Should().Be("my@key");
        tags[0].Value.Should().Be("value");
    }

    [Test]
    public void Sanitize_DropsTagWhenSanitizationProducesEmptyKey()
    {
        // All-invalid-chars key sanitises to "", which the final Where() filter drops so AWS
        // isn't sent a tag with an empty key.
        var userTags = new[]
        {
            new KeyValuePair<string, string>("!!!", "value"),
            new KeyValuePair<string, string>("validKey", "validValue")
        };

        var tags = EcsDefaultTags.Merge(new CalamariVariables(), userTags);

        tags.Should().ContainSingle().Which.Key.Should().Be("validKey");
    }

    [Test]
    public void Merge_PreservesOctopusVariableNamesDeclarationOrder()
    {
        var variables = new CalamariVariables();
        // Environment.Name is declared before Project.Name in OctopusVariableNames,
        // so it appears first in the output.
        variables.Set("Octopus.Environment.Name", "Production");
        variables.Set("Octopus.Project.Name", "MyProject");

        var userTags = new[] { new KeyValuePair<string, string>("Owner", "team@example.com") };

        var tags = EcsDefaultTags.Merge(variables, userTags);

        tags.Should().HaveCount(3);
        tags[0].Key.Should().Be("Octopus.Environment.Name");
        tags[1].Key.Should().Be("Octopus.Project.Name");
        tags[2].Key.Should().Be("Owner");
    }

    [Test]
    public void MergeTemplateTags_PriorityChain_TemplateBelowDefaultsBelowUser()
    {
        var v = new CalamariVariables();
        v.Set("Octopus.Project.Name", "fromDefault");
        v.Set("Octopus.Action.Name", "fromDefault");

        var templateTags = new[]
        {
            new Tag { Key = "CostCenter", Value = "fromTemplate" },
            new Tag { Key = "Octopus.Action.Name", Value = "fromTemplate" },
            new Tag { Key = "Env", Value = "fromTemplate" },
        };
        var userTags = new[]
        {
            new KeyValuePair<string, string>("Env", "fromUser"),
            new KeyValuePair<string, string>("Octopus.Project.Name", "fromUser"),
        };

        var tags = EcsDefaultTags.MergeAndDeduplicateTags(v, userTags, templateTags);

        tags.Should().BeEquivalentTo([
            new Tag { Key = "CostCenter", Value = "fromTemplate" },          // template-only carryforward
            new Tag { Key = "Octopus.Action.Name", Value = "fromDefault" },  // default beats template
            new Tag { Key = "Env", Value = "fromUser" },                     // user beats template
            new Tag { Key = "Octopus.Project.Name", Value = "fromUser" }     // user beats default
        ]);
    }
}
