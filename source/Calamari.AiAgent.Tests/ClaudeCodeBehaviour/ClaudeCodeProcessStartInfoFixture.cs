using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeProcessStartInfoFixture
{
    [TestCase("simple", "'simple'")]
    [TestCase("has space", "'has space'")]
    [TestCase("it's", @"'it'\''s'")]
    [TestCase("", "''")]
    [TestCase("a'b'c", @"'a'\''b'\''c'")]
    public void ShellQuote_QuotesCorrectly(string input, string expected)
    {
        ClaudeCodeProcessStartInfo.ShellQuote(input).Should().Be(expected);
    }

    const string TestSrtSettingsPath = "/tmp/test-workdir/srt-settings.json";

    static ClaudeCommandArgsBuilder MinimalBuilder() =>
        new ClaudeCommandArgsBuilder()
            .WithPrompt("test prompt")
            .WithModel("claude-sonnet-4-20250514");

    [Test]
    public void ResolveInvocation_NoneMode_RunsClaudeDirectly()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), SandboxMode.None);

        fileName.Should().Be("claude");
        arguments.Should().StartWith(" --model");
        arguments.Should().NotContain("srt");
    }

    [Test]
    public void ResolveInvocation_BashMode_RunsClaudeDirectly()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), SandboxMode.Bash);

        fileName.Should().Be("claude");
        arguments.Should().StartWith(" --model");
        arguments.Should().NotContain("srt");
    }

    [Test]
    public void ResolveInvocation_SrtMode_WrapsClaudeWithSrt()
    {
        var builder = MinimalBuilder().WithSrtSettingsPath(TestSrtSettingsPath);

        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(builder, SandboxMode.Srt);

        fileName.Should().Be("srt");
        arguments.Should().StartWith($"--settings {TestSrtSettingsPath} claude --model");
    }

    [Test]
    public void ResolveInvocation_SrtMode_WithoutSettingsPath_Throws()
    {
        var act = () => ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), SandboxMode.Srt);

        act.Should().Throw<System.InvalidOperationException>();
    }
}
