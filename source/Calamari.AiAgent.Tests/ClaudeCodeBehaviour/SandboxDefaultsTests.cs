using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SandboxDefaultsTests
{
    const string Key = "Test.List";
    static readonly string[] Defaults = { "default-a", "default-b" };

    [Test]
    public void Merge_NoUserEntries_ReturnsDefaults()
    {
        SandboxDefaults.Merge(new CalamariVariables(), Key, Defaults)
            .Should().BeEquivalentTo("default-a", "default-b");
    }

    [Test]
    public void Merge_CombinesUserEntriesWithDefaults()
    {
        var vars = new CalamariVariables();
        vars.Set(Key, "user-x\nuser-y");

        SandboxDefaults.Merge(vars, Key, Defaults)
            .Should().BeEquivalentTo("user-x", "user-y", "default-a", "default-b");
    }

    [Test]
    public void Merge_TrimsBlankLinesAndWhitespace_AndDedupes()
    {
        var vars = new CalamariVariables();
        vars.Set(Key, "  default-a \n\n user-x \n");

        SandboxDefaults.Merge(vars, Key, Defaults)
            .Should().BeEquivalentTo("default-a", "user-x", "default-b");
    }
}
