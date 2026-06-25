using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class InvokeClaudeCodeBehaviourFixture
{
    [Test]
    public void ResolvePermissionMode_DefaultsToDefault_WhenUnset()
    {
        var mode = InvokeClaudeCodeBehaviour.ResolvePermissionMode(new CalamariVariables());

        mode.Should().Be(ClaudePermissionMode.Default);
    }

    [Test]
    public void ResolvePermissionMode_ParsesAuto_CaseInsensitively()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.PermissionMode, "auto");

        InvokeClaudeCodeBehaviour.ResolvePermissionMode(vars).Should().Be(ClaudePermissionMode.Auto);
    }

    [Test]
    public void ResolvePermissionMode_ThrowsCommandException_ForUnknownValue()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.PermissionMode, "bogus");

        var act = () => InvokeClaudeCodeBehaviour.ResolvePermissionMode(vars);

        act.Should().Throw<CommandException>();
    }
}
